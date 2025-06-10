using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameInit : MonoBehaviour
{
    [SerializeField] GameObject ballPrefab; // Prefab for the pool balls
    [SerializeField] Transform cueBallPosition;
    [SerializeField] Transform firstBallPosition; // Position for the first ball in the rack
    
    [SerializeField] float ballDiameter = 0.06f; // 台球直径，可以在Inspector中调整
    [SerializeField] bool autoPlaceBalls = true; // 是否在游戏开始时自动放置球
    
    // 球的列表，用于跟踪游戏中的所有球
    private List<GameObject> balls = new List<GameObject>();
    
    // 静态字典存储所有球的网格
    private Dictionary<int, Mesh> ballMeshes = new Dictionary<int, Mesh>();
    
    // Start is called before the first frame update
    void Start()
    {
        // 首先加载所有球的网格
        LoadBallMeshes();
        
        if (autoPlaceBalls)
        {
            // Debug.Break(); // 调试用，暂停游戏以便检查
            PlaceAllBalls();
        }
    }
    
    void LoadBallMeshes()
    {
        // The correct path to the FBX model containing all ball meshes
        string fbxPath = "pool-table-traditional/source/pool_table_scene";
        
        // Load the entire model as a GameObject
        GameObject poolTableScene = Resources.Load<GameObject>(fbxPath);
        
        if (poolTableScene == null)
        {
            Debug.LogError("Failed to load pool table scene from path: " + fbxPath);
            return;
        }
        
        // Find all ball meshes inside the loaded model
        MeshFilter[] allMeshFilters = poolTableScene.GetComponentsInChildren<MeshFilter>(true);
        
        if (allMeshFilters.Length == 0)
        {
            Debug.LogError("No mesh filters found in the pool table scene.");
            return;
        }
        
        foreach (MeshFilter filter in allMeshFilters)
        {
            string name = filter.name.ToLower();
            if (name.Contains("billiard_ball"))
            {
                // Extract ball number from the mesh name
                int ballNumber = -1; // 初始化为无效值
                
                // 根据正确的映射关系设置球号
                if (name == "billiard_ball")
                {
                    ballNumber = 1; // 1号球对应 billiard_ball
                }
                else if (name == "billiard_ball015")
                {
                    ballNumber = 0; // 母球（0号）对应 billiard_ball015
                }
                else if (name.StartsWith("billiard_ball") && name.Length > "billiard_ball".Length)
                {
                    // 提取编号部分，例如从 billiard_ball001 中提取 001
                    string numberPart = name.Substring("billiard_ball".Length);
                    if (int.TryParse(numberPart, out int extractedNumber))
                    {
                        // 由于从001开始，所以要+1得到实际球号
                        ballNumber = extractedNumber + 1;
                    }
                }
                
                // Store the mesh
                if (ballNumber >= 0 && ballNumber <= 15)
                {
                    ballMeshes[ballNumber] = filter.sharedMesh;
                    Debug.Log("GameInit: Loaded mesh for ball " + ballNumber + ": " + name);
                }
                else
                {
                    Debug.LogWarning("Unknown ball mesh name format: " + name);
                }
            }
        }
        
        if (ballMeshes.Count == 0)
        {
            Debug.LogError("No ball meshes found in the pool table scene.");
        }
        else
        {
            Debug.Log("GameInit: Successfully loaded " + ballMeshes.Count + " ball meshes.");
        }
    }
    
    void PlaceAllBalls()
    {
        // 清除可能已存在的球
        ClearExistingBalls();
        
        PlaceCueBall();
        PlaceRackBalls();
    }
    
    void ClearExistingBalls()
    {
        foreach (GameObject ball in balls)
        {
            if (ball != null)
            {
                Destroy(ball);
            }
        }
        balls.Clear();
    }
    
    void PlaceCueBall()
    {
        if (ballPrefab == null || cueBallPosition == null)
        {
            Debug.LogError("Ball prefab or cue ball position not assigned!");
            return;
        }
        
        GameObject cueBall = Instantiate(ballPrefab, cueBallPosition.position, Quaternion.identity);
        ball ballComponent = cueBall.GetComponent<ball>();
        if (ballComponent != null)
        {
            // 直接设置球的类型和网格
            SetBallMeshAndProperties(ballComponent, 0);
        }
        balls.Add(cueBall);
        
        Debug.Log("Placed cue ball at " + cueBallPosition.position);
    }

    void PlaceRackBalls()
    {
        if (ballPrefab == null || firstBallPosition == null)
        {
            Debug.LogError("Ball prefab or first ball position not assigned!");
            return;
        }
        
        // 创建不重复的球号列表 (1-15)
        List<int> ballNumbers = new List<int>();
        for (int i = 1; i <= 15; i++)
        {
            if (i != 8) { // 排除8球，因为它有特殊位置
                ballNumbers.Add(i);
            }
        }
        
        ShuffleList(ballNumbers);
        
        // 三角形排列的5行
        Vector3 currentPos = firstBallPosition.position;
        
        // 修正: 行偏移为-z方向，乘以正确的系数
        Vector3 rowOffset = new Vector3(0, 0, -ballDiameter); // 行方向是-z
        
        // 修正: 列偏移为x方向
        Vector3 columnOffset = new Vector3(ballDiameter, 0, 0); // 列方向是x
        
        int ballIndex = 0;
        
        // 放置15个球的三角形排列
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col <= row; col++)
            {
                int ballNumber;
                
                // 8球放在中间位置 (第3排第2列)
                if (row == 2 && col == 1)
                {
                    ballNumber = 8;
                }
                else
                {
                    if (ballIndex >= ballNumbers.Count) return; // 安全检查
                    ballNumber = ballNumbers[ballIndex++];
                }
                
                // 计算球的位置:
                // 1. 从初始位置开始
                // 2. 沿z轴向后移动row行
                // 3. 沿x轴移动col列
                // 4. 每一行球的x位置需要向左偏移一点，使其能形成标准三角形
                float rowXOffset = row * ballDiameter / 2; // 每行的额外x偏移
                Vector3 position = currentPos 
                    + (rowOffset * row)        // 向后移动行数
                    + (columnOffset * col)     // 向右移动列数
                    - new Vector3(rowXOffset, 0, 0); // 根据行数向左偏移
                
                GameObject poolBall = Instantiate(ballPrefab, position, Quaternion.identity);
                
                ball ballComponent = poolBall.GetComponent<ball>();
                if (ballComponent != null)
                {
                    // 直接设置球的类型和网格
                    SetBallMeshAndProperties(ballComponent, ballNumber);
                }
                balls.Add(poolBall);
                
                Debug.Log("Placed ball " + ballNumber + " at " + position);
            }
        }
    }
    
    // 新方法：直接设置球的网格和属性
    void SetBallMeshAndProperties(ball ballComponent, int ballNumber)
    {
        ballComponent.makeBall(ballNumber); // 保留兼容性方法
        
        // 设置网格
        MeshFilter meshFilter = ballComponent.GetComponent<MeshFilter>();
        if (meshFilter != null && ballMeshes.TryGetValue(ballNumber, out Mesh ballMesh))
        {
            meshFilter.mesh = ballMesh;
        }
        else
        {
            Debug.LogError("Failed to set mesh for ball " + ballNumber);
        }
    }
    
    // 随机打乱列表的Fisher-Yates算法
    void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = 0; i < n; i++)
        {
            int r = i + Random.Range(0, n - i);
            T temp = list[i];
            list[i] = list[r];
            list[r] = temp;
        }
    }
    
    // 用于在游戏中重新开始时调用
    public void ResetGame()
    {
        PlaceAllBalls();
    }
    
    // Update is called once per frame
    void Update()
    {
        // 可以添加调试功能，例如按下特定键重置所有球
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetGame();
        }
    }
}
