using UnityEngine;

namespace Features.UI.Common
{
    public class CameraController : MonoBehaviour
    {
        // === 공개 변수 ===
        [Header("Camera Settings")]
        public Transform playerTransform; // Inspector에서 플레이어 오브젝트를 연결해줄 칸
        public float smoothSpeed = 0.125f; // 카메라가 따라오는 부드러운 정도
        public Vector3 offset; // 카메라와 플레이어 사이의 거리 오프셋

        void LateUpdate()
        {
            // 플레이어의 위치를 가져옴
            Vector3 desiredPosition = playerTransform.position + offset;

            // 카메라의 Z축 위치는 그대로 유지하면서 X, Y만 따라가도록 함
            desiredPosition.z = transform.position.z;

            // Vector3.Lerp를 사용해 현재 위치에서 목표 위치로 부드럽게 이동
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;
        }
    }
}