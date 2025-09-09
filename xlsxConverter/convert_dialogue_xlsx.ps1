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
        $csvData = Import-Csv -Path $InputPath -Encoding UTF8
        $cleanRows = [System.Collections.Generic.List[PSObject]]::new()
        $entryId = 0

        foreach ($csvRow in $csvData) {
            $entryId++
            $hasContent = $false
            
            # 모든 열(프로퍼티)을 순회하며 처리
            foreach ($property in $csvRow.PSObject.Properties) {
                $columnName = $property.Name
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
                            # Data1, Data2, ... 필드 동적 생성
                            for ($i = 0; $i -lt $maxDataFields; $i++) {
                                $newRow["Data$($i+1)"] = if ($i -lt $fields.Length) { $fields[$i] } else { '' }
                            }
                            $cleanRows.Add([PSCustomObject]$newRow)
                        }
                    }
                }
            }
             # 해당 ID에 아무런 내용이 없으면 빈 헤더 행을 추가하여 ID 유지
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

        # 최종 CSV의 헤더(모든 SourceColumn 이름)를 미리 수집
        $csvHeaders = $cleanData.$sourceCol | Select-Object -Unique | Where-Object { $_ -ne 'EMPTY_ROW' }

        $groupedById = $cleanData | Group-Object -Property $idCol
        
        $csvOutputRows = foreach ($idGroup in $groupedById) {
            $newCsvRow = [ordered]@{}
            
            # 미리 수집한 헤더를 기반으로 빈 값으로 초기화
            foreach($header in $csvHeaders) { $newCsvRow[$header] = "" }

            $groupedBySourceCol = $idGroup.Group | Group-Object -Property $sourceCol

            foreach ($sourceGroup in $groupedBySourceCol) {
                $columnName = $sourceGroup.Name
                if ($columnName -eq 'EMPTY_ROW') { continue }

                $items = foreach ($row in $sourceGroup.Group) {
                    $fields = @()
                    for ($i = 1; $i -le $maxDataFields; $i++) {
                        $fieldName = "Data$i"
                        # 해당 Data 필드가 존재하고, 비어있지 않은 경우에만 추가
                        if ($row.$fieldName) {
                            $fields += $row.$fieldName
                        }
                    }
                    # 필드가 하나라도 있어야 유효한 아이템으로 간주
                    if ($fields) { $fields -join '|' }
                }
                $newCsvRow[$columnName] = $items -join ';'
            }
            [PSCustomObject]$newCsvRow
        }

        # Select-Object를 사용하여 최종 열 순서 보장
        $csvOutputRows | Select-Object -Property $csvHeaders | Export-Csv -Path $OutputPath -NoTypeInformation -Encoding UTF8
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