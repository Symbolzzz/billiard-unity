using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;
    public Transform cameraTransform;
    public float mouseSensitivity = 100f; // Added for mouse look
    public float cameraPitchMin = -70f; // Added for camera pitch limits
    public float cameraPitchMax = 70f;  // Added for camera pitch limits
    
    // 摄像机碰撞检测参数
    public float cameraCollisionOffset = 0.5f; // 摄像机与障碍物的最小距离
    public float cameraCollisionRadius = 0.2f; // 摄像机碰撞检测半径
    public LayerMask cameraCollisionLayers = -1; // 摄像机碰撞检测层（默认为全部）
    public float cameraReturnSpeed = 8f; // 摄像机回到原始位置的速度
    public float cameraMinimumDistance = 1.5f; // 摄像机与玩家的最小距离
    
    // 增强的摄像机控制参数
    public float cameraFollowSpeed = 5f; // 摄像机跟随速度
    public float cameraLookAheadAmount = 1.2f; // 摄像机前瞻量
    public float cameraLookAheadSpeed = 2.0f; // 摄像机前瞻速度
    public float cameraDampingTime = 0.2f; // 摄像机阻尼时间
    public float cameraRotationSpeed = 3f; // 摄像机旋转速度
    
    private float currentCameraDistance; // 当前摄像机距离
    private Vector3 cameraVelocity; // 摄像机平滑移动速度
    private Vector3 cameraLookAheadVelocity; // 摄像机前瞻速度
    private Vector3 cameraLookAheadPoint; // 摄像机前瞻点
    private float cameraHeightVelocity; // 摄像机高度变化速度
    private Quaternion cameraRotationVelocity; // 摄像机旋转速度
    
    // 状态平滑参数，用于防止抖动
    public float groundedBufferTime = 0.1f; // 接地状态缓冲时间
    public float movementBufferTime = 0.2f; // 移动状态缓冲时间
    private float lastGroundedTime; // 最后接地的时间
    private float lastMovementTime; // 最后移动的时间
    private bool isMovingState; // 当前是否处于移动状态（经过平滑处理）
    
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private Vector3 cameraOffset; // Will be calculated as local offset
    private float currentCameraPitch = 0f; // Added for mouse look
    private PlayerAnimatorController playerAnimatorController; // Added for animations

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerAnimatorController = GetComponent<PlayerAnimatorController>(); // Get the PlayerAnimatorController component
        
        if (playerAnimatorController == null)
        {
            Debug.LogError("PlayerAnimatorController component not found on the player!");
        }

        if (cameraTransform != null)
        {
            // Calculate cameraOffset as a local offset from the player
            cameraOffset = Quaternion.Inverse(transform.rotation) * (cameraTransform.position - transform.position);
        }
        Cursor.lockState = CursorLockMode.Locked; // Lock cursor
        Cursor.visible = false; // Hide cursor
        
        lastGroundedTime = -groundedBufferTime; // 初始化为负值，确保一开始不会误判为接地
        lastMovementTime = -movementBufferTime; // 初始化为负值，确保一开始不会误判为移动
        currentCameraDistance = cameraOffset.magnitude; // 初始化为默认摄像机距离
        cameraLookAheadPoint = transform.position; // 初始化前瞻点
    }

    void Update()
    {
        // 地面检测与缓冲处理
        bool rawIsGrounded = controller.isGrounded;
        
        if (rawIsGrounded)
        {
            lastGroundedTime = Time.time;
        }
        
        isGrounded = rawIsGrounded || (Time.time - lastGroundedTime < groundedBufferTime);
        
        if (playerAnimatorController != null)
        {
            playerAnimatorController.SetGrounded(isGrounded);
        }

        if (isGrounded && rawIsGrounded) 
        {
            velocity.y = -2f; // 稳定接地
        }

        // 输入获取与移动处理
        float moveXInput = Input.GetAxis("Horizontal");
        float moveZInput = Input.GetAxis("Vertical");
        bool isRunningInput = Input.GetKey(KeyCode.LeftShift);

        Vector3 moveDirection = transform.right * moveXInput + transform.forward * moveZInput;
        float currentSpeed = isRunningInput ? runSpeed : walkSpeed;

        // 移动状态检测与缓冲处理
        bool rawIsMoving = moveDirection.magnitude > 0.1f;
        
        if (rawIsMoving)
        {
            lastMovementTime = Time.time;
        }
        
        isMovingState = rawIsMoving || (Time.time - lastMovementTime < movementBufferTime);
        
        // 获取当前是否处于跳跃动画中
        bool isCurrentlyJumping = false;
        if (playerAnimatorController != null)
        {
            isCurrentlyJumping = playerAnimatorController.GetJumpingState();
        }

        // 动画状态更新
        if (playerAnimatorController != null)
        {
            // 如果在地面上，根据移动状态设置跑步动画
            if (isGrounded)
            {
                // 只有在非跳跃状态时更新Running参数
                if (!isCurrentlyJumping)
                {
                    playerAnimatorController.SetRunning(isMovingState);
                }
                
                // 如果已经接地并且不是刚刚跳起，确保跳跃动画关闭
                if (velocity.y <= 0)
                {
                    playerAnimatorController.SetJumping(false);
                }
            }
            // 不在地面时，保持当前的Running状态，这样空中可以继续显示移动姿态
        }

        // 应用移动
        if (rawIsMoving) // 使用原始移动检测来实际移动角色
        {
            controller.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);
        }

        // 跳跃
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (playerAnimatorController != null) 
            {
                playerAnimatorController.SetJumping(true);
                // 不再设置 SetRunning(false)，保持当前移动状态
            }
        }

        // 重力
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Mouse Look Input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Player Yaw (Horizontal rotation)
        transform.Rotate(Vector3.up * mouseX);

        // Camera Pitch (Vertical rotation)
        currentCameraPitch -= mouseY;
        currentCameraPitch = Mathf.Clamp(currentCameraPitch, cameraPitchMin, cameraPitchMax);
    }

    void LateUpdate() // Modified for camera follow and rotation
    {
        if (cameraTransform != null)
        {
            // 获取移动输入，用于计算前瞻方向
            float moveXInput = Input.GetAxis("Horizontal");
            float moveZInput = Input.GetAxis("Vertical");
            bool hasMovementInput = Mathf.Abs(moveXInput) > 0.1f || Mathf.Abs(moveZInput) > 0.1f;
            
            // 计算前瞻点 - 当玩家移动时，摄像机会稍微前瞻一些
            Vector3 lookAheadDirection = transform.right * moveXInput + transform.forward * moveZInput;
            Vector3 targetLookAheadPoint = transform.position;
            
            if (hasMovementInput && isGrounded)
            {
                // 在有移动输入时计算前瞻点
                targetLookAheadPoint = transform.position + lookAheadDirection.normalized * cameraLookAheadAmount;
            }
            
            // 平滑过渡到新的前瞻点
            cameraLookAheadPoint = Vector3.SmoothDamp(
                cameraLookAheadPoint, 
                targetLookAheadPoint, 
                ref cameraLookAheadVelocity, 
                cameraDampingTime
            );
            
            // 确定相机的目标位置
            Vector3 targetPosition = cameraLookAheadPoint + (transform.rotation * cameraOffset.normalized * currentCameraDistance);
            
            // 计算射线起点 (从角色头部开始射线，而不是从角色中心点)
            float characterHeight = controller.height;
            Vector3 rayOrigin = transform.position + Vector3.up * (characterHeight * 0.6f);
            
            // 进行摄像机碰撞检测
            RaycastHit hit;
            Vector3 cameraDirection = (targetPosition - rayOrigin).normalized;
            float distanceToTarget = Vector3.Distance(rayOrigin, targetPosition);
            
            // 从头部位置向摄像机位置发射射线
            bool isColliding = Physics.SphereCast(
                rayOrigin, 
                cameraCollisionRadius, 
                cameraDirection, 
                out hit, 
                distanceToTarget, 
                cameraCollisionLayers
            );
            
            // 处理碰撞
            if (isColliding)
            {
                float distanceWithOffset = hit.distance - cameraCollisionOffset;
                distanceWithOffset = Mathf.Max(cameraMinimumDistance, distanceWithOffset);
                currentCameraDistance = Mathf.SmoothDamp(
                    currentCameraDistance, 
                    distanceWithOffset, 
                    ref cameraHeightVelocity, 
                    0.2f
                );
            }
            else
            {
                float originalDistance = cameraOffset.magnitude;
                currentCameraDistance = Mathf.SmoothDamp(
                    currentCameraDistance, 
                    originalDistance, 
                    ref cameraHeightVelocity, 
                    0.5f
                );
            }
            
            // 根据玩家移动、高度变化等动态调整摄像机位置
            float heightOffset = cameraOffset.y;
            
            // 如果玩家正在跳跃，稍微增加摄像机高度
            if (!isGrounded)
            {
                heightOffset += 0.3f * Mathf.Abs(velocity.y);
            }
            
            // 计算摄像机最终位置
            Vector3 forwardDirection = transform.rotation * Vector3.Scale(cameraOffset.normalized, new Vector3(1, 0, 1)).normalized;
            Vector3 targetCameraPosition = transform.position + Vector3.up * heightOffset + forwardDirection * currentCameraDistance;
            
            // 平滑移动摄像机到目标位置
            cameraTransform.position = Vector3.SmoothDamp(
                cameraTransform.position, 
                targetCameraPosition, 
                ref cameraVelocity, 
                1.0f / cameraFollowSpeed
            );
            
            // 计算目标旋转 - 考虑了角色的旋转和摄像机俯仰
            Quaternion targetRotation = transform.rotation * Quaternion.Euler(currentCameraPitch, 0f, 0f);
            
            // 当玩家快速旋转或移动时稍微倾斜摄像机
            if (hasMovementInput)
            {
                float tiltAmount = moveXInput * 2.0f; // 最大倾斜度
                targetRotation *= Quaternion.Euler(0f, 0f, -tiltAmount);
            }
            
            // 平滑过渡到新的旋转
            cameraTransform.rotation = Quaternion.Slerp(
                cameraTransform.rotation, 
                targetRotation, 
                Time.deltaTime * cameraRotationSpeed
            );
        }
    }
}