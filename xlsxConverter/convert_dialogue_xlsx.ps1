[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('to-xlsx', 'to-csv')]
    [string]$Mode,
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

# ==========================================================
# ImportExcel 모듈을 '전체 경로'로 직접 불러옵니다.
# !!! 중요: 'bswan0113' 부분을 본인의 Windows 사용자 이름으로 변경하세요. !!!
# ==========================================================
$modulePath = "C:\Users\bswan0113\Documents\WindowsPowerShell\Modules\ImportExcel"
try {
    Import-Module -Name $modulePath -ErrorAction Stop
}
catch {
    Write-Error "CRITICAL ERROR: The 'ImportExcel' module could not be loaded from path '$modulePath'."
    Start-Sleep -Seconds 5
    exit 1
}

# --- CONFIGURATION ---
$cleanSheetName = 'ConvertedData'
$idCol = 'EntryID'
$sourceCol = 'SourceColumn'
# 데이터 필드를 최대 몇 개까지 지원할지 결정 (예: 5개면 Data1 ~ Data5)
$maxDataFields = 5


function Convert-CsvToXlsx {
    Write-Host "Converting '$InputPath' (CSV) -> '$OutputPath' (XLSX)..." -ForegroundColor Green
    try {
        # 'id' 열이 있는 경우를 대비해 Import-Csv 전에 헤더를 직접 읽어서 사용
        $fileContent = Get-Content -Path $InputPath -Encoding UTF8
        $headers = $fileContent[0].Split(',')
        $csvData = $fileContent | Select-Object -Skip 1 | ConvertFrom-Csv -Header $headers

        $cleanRows = [System.Collections.Generic.List[PSObject]]::new()
        
        foreach ($csvRow in $csvData) {
            # id 열이 있는지 확인하고 있으면 사용, 없으면 순차적으로 생성
            $entryId = if ($csvRow.PSObject.Properties['id']) { $csvRow.id } else { $cleanRows.Count + 1 }
            $hasContent = $false
            
            foreach ($property in $csvRow.PSObject.Properties) {
                $columnName = $property.Name
                # id 열은 SourceColumn으로 만들지 않도록 건너뜀
                if ($columnName -eq 'id') { continue }

                $cellValue = $property.Value
                if (-not [string]::IsNullOrWhiteSpace($cellValue)) {
                    $hasContent = $true
                    $items = $cellValue.Split(';')
                    foreach ($item in $items) {
                        if (-not [string]::IsNullOrWhiteSpace($item)) {
                            $fields = $item.Split('|')
                            $newRow = [ordered]@{
                                $idCol = $entryId
                                $sourceCol = $columnName
                            }
                            for ($i = 0; $i -lt $maxDataFields; $i++) {
                                $newRow["Data$($i+1)"] = if ($i -lt $fields.Length) { $fields[$i] } else { '' }
                            }
                            $cleanRows.Add([PSCustomObject]$newRow)
                        }
                    }
                }
            }
            if (-not $hasContent) {
                 $newRow = [ordered]@{ $idCol = $entryId; $sourceCol = 'EMPTY_ROW' }
                 for ($i = 0; $i -lt $maxDataFields; $i++) { $newRow["Data$($i+1)"] = '' }
                 $cleanRows.Add([PSCustomObject]$newRow)
            }
        }
        
        $cleanRows | Export-Excel -Path $OutputPath -WorksheetName $cleanSheetName -AutoSize -AutoFilter -FreezeTopRow
        Write-Host "Conversion successful." -ForegroundColor Green
    } catch {
        Write-Error "An error occurred during 'to-xlsx' conversion: $_"
    }
}

function Convert-XlsxToCsv {
    Write-Host "Converting '$InputPath' (XLSX) -> '$OutputPath' (CSV)..." -ForegroundColor Cyan
    try {
        $cleanData = Import-Excel -Path $InputPath -WorksheetName $cleanSheetName
        if (-not $cleanData) { throw "No data found in worksheet '$cleanSheetName'." }

        # id열과 sourceCol 열을 제외한 모든 열을 최종 헤더로 사용
        $csvHeaders = $cleanData.$sourceCol | Select-Object -Unique | Where-Object { $_ -ne 'EMPTY_ROW' }

        $groupedById = $cleanData | Group-Object -Property $idCol
        
        $csvOutputRows = foreach ($idGroup in $groupedById) {
            # 최종 CSV 행 객체 생성, id를 첫 번째 열로 추가
            $newCsvRow = [ordered]@{ 'id' = $idGroup.Name }
            
            foreach($header in $csvHeaders) { $newCsvRow[$header] = "" }

            $groupedBySourceCol = $idGroup.Group | Group-Object -Property $sourceCol

            foreach ($sourceGroup in $groupedBySourceCol) {
                $columnName = $sourceGroup.Name
                if ($columnName -eq 'EMPTY_ROW') { continue }

                $items = foreach ($row in $sourceGroup.Group) {
                    $fields = @()
                    for ($i = 1; $i -le $maxDataFields; $i++) {
                        $fieldName = "Data$i"
                        if ($row.PSObject.Properties[$fieldName] -and $row.$fieldName) {
                            $fields += $row.$fieldName
                        }
                    }
                    if ($fields) { $fields -join '|' }
                }
                $newCsvRow[$columnName] = $items -join ';'
            }
            [PSCustomObject]$newCsvRow
        }

        # ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
        # --- 여기가 수정된 부분입니다 ---
        # Export-Csv 대신, ConvertTo-Csv로 문자열로 변환 후 직접 따옴표를 제거
        
        # 1. 헤더 순서를 'id' 다음에 나머지 헤더가 오도록 정의
        $finalHeaders = @('id') + $csvHeaders
        
        # 2. 데이터를 문자열 배열로 변환
        $csvContent = $csvOutputRows | Select-Object -Property $finalHeaders | ConvertTo-Csv -NoTypeInformation

        # 3. 첫 번째 줄(헤더)에서만 따옴표(")를 모두 제거
        $csvContent[0] = $csvContent[0] -replace '"', ''

        # 4. 수정된 내용을 파일에 저장
        $csvContent | Out-File -FilePath $OutputPath -Encoding UTF8
        # ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        Write-Host "Conversion successful." -ForegroundColor Cyan
    } catch {
        Write-Error "An error occurred during 'to-csv' conversion: $_"
    }
}


if (-not (Test-Path -Path $InputPath)) {
    Write-Error "Input file not found at '$InputPath'"
    exit 1
}
switch ($Mode) {
    'to-xlsx' { Convert-CsvToXlsx }
    'to-csv'  { Convert-XlsxToCsv }
}