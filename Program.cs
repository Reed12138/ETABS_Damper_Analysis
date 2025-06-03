using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ETABSv17;
using System.Diagnostics; // 用于计时器

namespace ETABS_Damper_Analysis
{
    class Program
    {
        static Random rand = new Random();

        static void Main(string[] args)
        {
            // 启动计时器
            Stopwatch stopwatch = Stopwatch.StartNew();

            string modelPath = "E:\\Wenyi12138\\0306_night_C#\\ETABS_model\\模型.edb";
            string outputDir = "E:\\Wenyi12138\\0306_night_C#\\Output";
            string driftMaxDir = Path.Combine(outputDir, "driftmax"); // 创建driftmax子文件夹
            string driftAverageDir = Path.Combine(outputDir, "driftaverage"); // 创建driftaverage子文件夹
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(driftMaxDir);
            Directory.CreateDirectory(driftAverageDir);

            // 定义结果文件路径
            string driftMaxFile = Path.Combine(driftMaxDir, "results_driftmax.csv");
            string driftAverageFile = Path.Combine(driftAverageDir, "results_driftaverage.csv");

            // 初始化结果文件（写入标题行）
            if (!File.Exists(driftMaxFile))
            {
                File.WriteAllText(driftMaxFile, "GM_name,damper_state,C_alpha_arrange,Output\n");
            }
            if (!File.Exists(driftAverageFile))
            {
                File.WriteAllText(driftAverageFile, "GM_name,damper_state,C_alpha_arrange,Output\n");
            }

            // 初始化ETABS
            cOAPI myETABSObject = InitializeETABS();
            if (myETABSObject == null)
            {
                Console.WriteLine("ETABS初始化失败，程序退出。");
                return;
            }

            cSapModel mySapModel = myETABSObject.SapModel;

            // 打开现有模型
            int ret = mySapModel.File.OpenFile(modelPath);
            if (ret != 0)
            {
                Console.WriteLine("无法打开模型文件: " + modelPath);
                myETABSObject.ApplicationExit(false);
                return;
            }

            // 验证楼层定义
            int storyCount = 0;
            string[] storyNames = null;
            ret = mySapModel.Story.GetNameList(ref storyCount, ref storyNames);
            Console.WriteLine($"楼层数量: {storyCount}, 楼层列表: {string.Join(", ", storyNames ?? new string[0])}");

            // 预定义所有阻尼器属性
            List<DamperProperty> damperProperties = LoadDamperProperties("E:\\Wenyi12138\\0306_night_C#\\dampers.csv");
            foreach (var prop in damperProperties)
            {
                ret = DefineDamperProperty(mySapModel, prop);
                if (ret != 0) Console.WriteLine($"定义阻尼器属性失败，Name: {prop.Name}");
            }

            // 加载节点信息
            Dictionary<int, string[]> nodeMap = LoadNodeMap("E:\\Wenyi12138\\0306_night_C#\\nodes.csv");

            // 加载工况名
            List<string> runCaseFlags = LoadRunCaseFlags("E:\\Wenyi12138\\0306_night_C#\\runcaseflag_validation.csv");

            // 手动输入阻尼器布置和参数设置（临时使用）
            int[] manualDamperState = new int[] { 3, 1, 0, 1 };
            int[] manualCAlphaArrange = new int[] { 1, 6, 0, 2 };

            // 生成随机配置（100次，暂时注释）
            for (int i = 0; i < 1; i++)
            {
                // 每次循环开始前，清空所有阻尼器
                ret = ClearDampers(mySapModel);
                if (ret != 0)
                {
                    Console.WriteLine($"清空阻尼器失败，ID: {i}");
                    continue;
                }

                // 使用手动输入的配置替代自动随机生成
                DamperConfig config = new DamperConfig
                {
                    ID = i.ToString(),
                    DamperState = manualDamperState,
                    CAlphaArrange = manualCAlphaArrange
                };

                // 部署阻尼器（注释掉自动部署，使用手动部署）
                //DamperConfig config = GenerateRandomConfig(i.ToString(), damperProperties.Count);
                //ret = DeployDampers(mySapModel, nodeMap, config);
                ret = ManualDeployDampers(mySapModel, nodeMap, manualDamperState, manualCAlphaArrange);
                if (ret != 0)
                {
                    Console.WriteLine($"部署阻尼器失败，ID: {config.ID}");
                    continue;
                }
                else
                {
                    // 打印阻尼器配置信息
                    Console.WriteLine($"阻尼器配置成功，ID: {config.ID}, DamperState: [{string.Join(", ", config.DamperState)}], CAlphaArrange: [{string.Join(", ", config.CAlphaArrange)}]");
                }

                // 遍历所有工况
                foreach (var selectedCase in runCaseFlags)
                {
                    foreach (var caseName in runCaseFlags)
                    {
                        bool run = caseName == selectedCase;
                        ret = mySapModel.Analyze.SetRunCaseFlag(caseName, run);
                        if (ret != 0)
                        {
                            Console.WriteLine($"设置工况 {caseName} 失败，ID: {config.ID}");
                            continue;
                        }
                    }

                    // 运行分析
                    ret = mySapModel.Analyze.RunAnalysis();
                    if (ret != 0)
                    {
                        Console.WriteLine($"分析失败，ID: {config.ID}, 工况: {selectedCase}");
                        continue;
                    }

                    // 取消所有工况和组合的输出选择
                    ret = mySapModel.Results.Setup.DeselectAllCasesAndCombosForOutput();
                    if (ret != 0)
                    {
                        Console.WriteLine($"取消输出选择失败，ID: {config.ID}, 工况: {selectedCase}");
                        continue;
                    }

                    // 设置当前工况为输出
                    ret = mySapModel.Results.Setup.SetCaseSelectedForOutput(selectedCase, true);
                    if (ret != 0)
                    {
                        Console.WriteLine($"设置工况输出失败，ID: {config.ID}, 工况: {selectedCase}");
                        continue;
                    }

                    // 设置直接历史输出为 Step-by-Step
                    ret = mySapModel.Results.Setup.SetOptionDirectHist(2);
                    if (ret != 0)
                    {
                        Console.WriteLine($"设置直接历史输出失败，ID: {config.ID}, 工况: {selectedCase}");
                        continue;
                    }

                    // 提取节点位移结果
                    var displacements = GetJointDisplacements(mySapModel, selectedCase);
                    if (displacements == null)
                    {
                        Console.WriteLine($"提取位移失败，ID: {config.ID}, 工况: {selectedCase}");
                        continue;
                    }

                    // 计算平均最大层间位移角和每榀框架的最大层间位移角
                    var (averageOutput, maxOutput) = CalculateInterstoryDrifts(displacements);
                    if (averageOutput == null || maxOutput == null)
                    {
                        Console.WriteLine($"计算层间位移角失败，ID: {config.ID}, 工况: {selectedCase}");
                        continue;
                    }

                    // 保存结果
                    //string resultFile = Path.Combine(outputDir, $"results_{config.ID}_{selectedCase}_displacements.csv");
                    //SaveDisplacements(displacements, resultFile);
                    //Console.WriteLine($"完成位移记录，ID: {config.ID}, 工况: {selectedCase}");

                    // 保存最大层间位移角和平均最大层间位移角
                    SaveInterstoryDrifts(selectedCase, maxOutput, config.DamperState, config.CAlphaArrange, driftMaxFile, true);
                    SaveInterstoryDrifts(selectedCase, averageOutput, config.DamperState, config.CAlphaArrange, driftAverageFile, false);
                    Console.WriteLine($"完成层间位移角计算，ID: {config.ID}, 工况: {selectedCase}");

                    // 保存结果后解锁模型
                    ret = mySapModel.SetModelIsLocked(false);
                    if (ret != 0)
                    {
                        Console.WriteLine($"解锁模型失败，ID: {config.ID}, 工况: {selectedCase}");
                        continue;
                    }
                }
            }

            // 任务完成后关闭ETABS
            myETABSObject.ApplicationExit(false);

            // 停止计时器并输出总用时
            stopwatch.Stop();
            double totalSeconds = stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"所有任务完成，总用时: {totalSeconds:F2} 秒。");
            Console.ReadLine();
        }

        // 初始化ETABS
        static cOAPI InitializeETABS()
        {
            cHelper myHelper = new Helper();
            cOAPI myETABSObject = myHelper.CreateObjectProgID("CSI.ETABS.API.ETABSObject");
            int ret = myETABSObject.ApplicationStart();
            return ret == 0 ? myETABSObject : null;
        }

        // 定义阻尼器属性
        static int DefineDamperProperty(cSapModel model, DamperProperty prop)
        {
            bool[] dof = new bool[6] { true, false, false, false, false, false };
            bool[] Fixed = new bool[6] { false, false, false, false, false, false };
            bool[] nonlinear = new bool[6] { true, false, false, false, false, false };
            double[] ke = new double[6] { prop.Ke, 0, 0, 0, 0, 0 };
            double[] ce = new double[6] { prop.Ce, 0, 0, 0, 0, 0 };
            double[] k = new double[6] { prop.K, 0, 0, 0, 0, 0 };
            double[] c = new double[6] { prop.C, 0, 0, 0, 0, 0 };
            double[] cExp = new double[6] { 0.30, 0, 0, 0, 0, 0 }; // 阻尼指数恒定为0.30
            double dj2 = 0, dj3 = 0;

            return model.PropLink.SetDamper(prop.Name, ref dof, ref Fixed, ref nonlinear, ref ke, ref ce, ref k, ref c, ref cExp, dj2, dj3);
        }

        // 清空所有阻尼器（链接对象）
        static int ClearDampers(cSapModel model)
        {
            int ret = 0;
            int numberNames = 0;
            string[] linkNames = null;

            // 获取所有链接对象名称
            ret = model.LinkObj.GetNameList(ref numberNames, ref linkNames);
            if (ret != 0 || numberNames == 0)
            {
                // 如果没有链接对象，直接返回
                return ret;
            }

            // 删除所有链接对象
            for (int i = 0; i < numberNames; i++)
            {
                ret = model.LinkObj.Delete(linkNames[i], eItemType.Objects);
                if (ret != 0)
                {
                    Console.WriteLine($"删除链接对象 {linkNames[i]} 失败");
                    return ret;
                }
            }

            return ret;
        }

        // 手动部署阻尼器，根据用户输入的阻尼器布置和参数设置
        static int ManualDeployDampers(cSapModel model, Dictionary<int, string[]> nodeMap, int[] damperState, int[] cAlphaArrange)
        {
            int ret = 0;
            int bayCount = 7; // bay1到bay7
            int[] baseNodes = { 1, 2, 3 }; // 底层节点
            int[] targetNodes = { 12, 13, 14 }; // 第二层节点

            // 验证输入数组长度一致
            if (damperState.Length != cAlphaArrange.Length)
            {
                Console.WriteLine("错误：damperState 和 cAlphaArrange 数组长度不一致！");
                return -1;
            }

            for (int story = 0; story < damperState.Length; story++)
            {
                int damperCount = damperState[story];
                string propName = cAlphaArrange[story].ToString();

                for (int bay = 0; bay < bayCount; bay++)
                {
                    for (int d = 0; d < damperCount && d < baseNodes.Length; d++)
                    {
                        string point1 = nodeMap[baseNodes[d]][bay];
                        string point2 = nodeMap[targetNodes[d]][bay];

                        if (point1 == "N" || point2 == "N") continue;

                        string linkName = "";
                        ret = model.LinkObj.AddByPoint(point1, point2, ref linkName, false, propName);
                        if (ret != 0)
                        {
                            Console.WriteLine($"部署阻尼器失败，Story: {story}, Bay: {bay}, Node: {point1}-{point2}");
                            return ret;
                        }
                    }
                }

                // 更新节点对
                if (story < damperState.Length - 1)
                {
                    baseNodes = new int[] { baseNodes[0] + 10, baseNodes[1] + 10, baseNodes[2] + 10 };
                    targetNodes = new int[] { targetNodes[0] + 10, targetNodes[1] + 10, targetNodes[2] + 10 };
                }
            }
            return ret;
        }

        // 部署阻尼器（保留，暂时注释）
        static int DeployDampers(cSapModel model, Dictionary<int, string[]> nodeMap, DamperConfig config)
        {
            int ret = 0;
            int bayCount = 7; // bay1到bay7
            int[] baseNodes = { 1, 2, 3 }; // 底层节点
            int[] targetNodes = { 12, 13, 14 }; // 第二层节点

            for (int story = 0; story < config.DamperState.Length; story++)
            {
                int damperCount = config.DamperState[story];
                string propName = config.CAlphaArrange[story].ToString();

                for (int bay = 0; bay < bayCount; bay++)
                {
                    for (int d = 0; d < damperCount && d < baseNodes.Length; d++)
                    {
                        string point1 = nodeMap[baseNodes[d]][bay];
                        string point2 = nodeMap[targetNodes[d]][bay];

                        if (point1 == "N" || point2 == "N") continue;

                        string linkName = "";
                        ret = model.LinkObj.AddByPoint(point1, point2, ref linkName, false, propName);
                        if (ret != 0) return ret;
                    }
                }

                // 更新节点对
                if (story < config.DamperState.Length - 1)
                {
                    baseNodes = new int[] { baseNodes[0] + 10, baseNodes[1] + 10, baseNodes[2] + 10 };
                    targetNodes = new int[] { targetNodes[0] + 10, targetNodes[1] + 10, targetNodes[2] + 10 };
                }
            }
            return ret;
        }

        // 提取节点位移结果
        static Dictionary<string, Dictionary<string, DisplacementData>> GetJointDisplacements(cSapModel model, string loadCase)
        {
            int numberResults = 0;
            string[] obj = null;
            string[] elm = null;
            string[] loadCases = null;
            string[] stepType = null;
            double[] stepNum = null;
            double[] u1 = null;
            double[] u2 = null;
            double[] u3 = null;
            double[] r1 = null;
            double[] r2 = null;
            double[] r3 = null;

            // 定义需要记录的节点（28个节点）
            string[,] nodeGroups = new string[5, 7] {
                { "22", "28", "27", "26", "25", "24", "23" }, // 层1 (节点1固定为0)
                { "67", "66", "76", "78", "80", "82", "87" }, // 层2
                { "115", "121", "120", "119", "118", "117", "116" }, // 层3
                { "198", "197", "207", "209", "211", "213", "218" }, // 层4
                { "266", "267", "268", "270", "259", "258", "257" }  // 层5
            };

            var displacements = new Dictionary<string, Dictionary<string, DisplacementData>>();
            var allStepNums = new HashSet<double>(); // 用于收集所有时间步

            // 遍历每个节点组
            for (int level = 0; level < 5; level++)
            {
                for (int bay = 0; bay < 7; bay++)
                {
                    string nodeName = nodeGroups[level, bay];
                    if (string.IsNullOrEmpty(nodeName)) continue;

                    // 使用 JointDispl 提取位移
                    int ret = model.Results.JointDispl(nodeName, eItemTypeElm.ObjectElm, ref numberResults, ref obj, ref elm,
                        ref loadCases, ref stepType, ref stepNum, ref u1, ref u2, ref u3, ref r1, ref r2, ref r3);

                    if (ret != 0 || numberResults == 0)
                    {
                        Console.WriteLine($"JointDispl 返回错误码: {ret}, 结果数量: {numberResults}, 节点: {nodeName}, 工况: {loadCase}");
                        continue;
                    }

                    var nodeData = new Dictionary<string, DisplacementData>();
                    for (int i = 0; i < numberResults; i++)
                    {
                        if (loadCases[i] == loadCase)
                        {
                            string stepKey = stepNum[i].ToString();
                            allStepNums.Add(stepNum[i]); // 收集所有时间步
                            nodeData[stepKey] = new DisplacementData
                            {
                                LoadCase = loadCases[i],
                                StepNum = stepNum[i],
                                U1 = u1[i],
                                U2 = u2[i],
                                U3 = u3[i],
                                R1 = r1[i],
                                R2 = r2[i],
                                R3 = r3[i]
                            };
                        }
                    }
                    displacements[nodeName] = nodeData;
                }
            }

            // 第一层节点（位置1）位移设为0
            for (int bay = 0; bay < 7; bay++)
            {
                string nodeName = nodeGroups[0, bay];
                if (!string.IsNullOrEmpty(nodeName))
                {
                    displacements[nodeName] = new Dictionary<string, DisplacementData>
                    {
                        { "0", new DisplacementData { LoadCase = loadCase, StepNum = 0, U1 = 0, U2 = 0, U3 = 0, R1 = 0, R2 = 0, R3 = 0 } }
                    };
                    allStepNums.Add(0); // 确保包含0步
                }
            }

            // 将所有时间步转换为数组，供后续使用
            Program.allStepNums = allStepNums.ToArray();

            return displacements.Count > 0 ? displacements : null;
        }

        // 静态变量保存所有时间步
        public static double[] allStepNums;

        // 计算平均最大层间位移角和每榀框架的最大层间位移角
        static (double[], double[]) CalculateInterstoryDrifts(Dictionary<string, Dictionary<string, DisplacementData>> displacements)
        {
            if (displacements == null || displacements.Count == 0) return (null, null);

            string[,] nodeGroups = new string[5, 7] {
                { "22", "28", "27", "26", "25", "24", "23" },
                { "67", "66", "76", "78", "80", "82", "87" },
                { "115", "121", "120", "119", "118", "117", "116" },
                { "198", "197", "207", "209", "211", "213", "218" },
                { "266", "267", "268", "270", "259", "258", "257" }
            };

            double[] storyHeights = new double[] { 5650, 5100, 5100, 5900 }; // 层高数组，单位mm
            double[] averageOutput = new double[4]; // STORY1到STORY4 的平均最大层间位移角
            double[] maxOutput = new double[4]; // STORY1到STORY4 的每榀框架最大层间位移角

            // 使用所有节点的时间步
            double[] stepNums = Program.allStepNums ?? new double[0];
            if (stepNums.Length == 0)
            {
                Console.WriteLine("未找到有效时间步数据。");
                return (null, null);
            }

            // 存储每个 bay 在整个时间历程中的最大位移差
            double[,] bayMaxDrifts = new double[4, 7]; // [story, bay]

            foreach (double stepNum in stepNums)
            {
                for (int story = 0; story < 4; story++) // 4层
                {
                    double[] bayDriftDiffs = new double[7]; // 存储每个 bay 的层间位移差值（当前时间步）

                    for (int bay = 0; bay < 7; bay++)
                    {
                        string upperNode = nodeGroups[story + 1, bay];
                        string lowerNode = nodeGroups[story, bay];
                        if (string.IsNullOrEmpty(upperNode) || string.IsNullOrEmpty(lowerNode)) continue;

                        DisplacementData upperData = displacements[upperNode].ContainsKey(stepNum.ToString())
                            ? displacements[upperNode][stepNum.ToString()]
                            : new DisplacementData { U2 = 0 };
                        DisplacementData lowerData = displacements[lowerNode].ContainsKey(stepNum.ToString())
                            ? displacements[lowerNode][stepNum.ToString()]
                            : new DisplacementData { U2 = 0 };

                        double driftDiff = upperData.U2 - lowerData.U2;
                        double absDriftDiff = Math.Abs(driftDiff);

                        // 更新该 bay 的最大位移差
                        bayMaxDrifts[story, bay] = Math.Max(bayMaxDrifts[story, bay], absDriftDiff);

                        // 存储当前时间步的位移差，用于计算平均最大层间位移角
                        bayDriftDiffs[bay] = absDriftDiff;
                    }

                    // 计算当前时间步的平均最大层间位移角
                    double avgMaxDriftDiff = bayDriftDiffs.Average();
                    if (!double.IsNaN(avgMaxDriftDiff) && avgMaxDriftDiff > 0)
                    {
                        double avgDrift = avgMaxDriftDiff / storyHeights[story];
                        averageOutput[story] = Math.Max(averageOutput[story], Math.Abs(avgDrift));
                    }
                }
            }

            // 计算每层楼的最大层间位移角（基于每个 bay 的最大位移差）
            for (int story = 0; story < 4; story++)
            {
                double[] storyBayMaxDrifts = new double[7];
                for (int bay = 0; bay < 7; bay++)
                {
                    storyBayMaxDrifts[bay] = bayMaxDrifts[story, bay] / storyHeights[story];
                }
                maxOutput[story] = storyBayMaxDrifts.Max(); // 取每层楼所有 bay 中的最大值
            }

            return (averageOutput, maxOutput);
        }

        // 保存节点位移结果
        static void SaveDisplacements(Dictionary<string, Dictionary<string, DisplacementData>> displacements, string filePath)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine("Node,StepNum,LoadCase,U1,U2,U3,R1,R2,R3");
                foreach (var nodeEntry in displacements)
                {
                    string nodeName = nodeEntry.Key;
                    foreach (var stepEntry in nodeEntry.Value)
                    {
                        string stepNum = stepEntry.Key;
                        DisplacementData data = stepEntry.Value;
                        sw.WriteLine($"{nodeName},{stepNum},{data.LoadCase},{data.U1},{data.U2},{data.U3},{data.R1},{data.R2},{data.R3}");
                    }
                }
            }
        }

        // 保存层间位移角（最大或平均）
        static void SaveInterstoryDrifts(string gmName, double[] output, int[] damperState, int[] cAlphaArrange, string filePath, bool isMax)
        {
            using (StreamWriter sw = new StreamWriter(filePath, true)) // 追加模式
            {
                // 写入数据行，GM_name, damper_state, C_alpha_arrange 和 Output 作为数组字符串
                string damperStateStr = $"[{string.Join(", ", damperState)}]";
                string cAlphaArrangeStr = $"[{string.Join(", ", cAlphaArrange)}]";
                string outputStr = $"[{string.Join(", ", output.Select(d => d.ToString("F14")))}]";
                sw.WriteLine($"\"{gmName}\",\"{damperStateStr}\",\"{cAlphaArrangeStr}\",\"{outputStr}\"");
            }
        }

        // 加载节点对照表
        static Dictionary<int, string[]> LoadNodeMap(string filePath)
        {
            var nodeMap = new Dictionary<int, string[]>();
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(','); // 逗号分隔
                int nodeId = int.Parse(parts[0]);
                string[] bayNodes = new string[7];
                for (int j = 0; j < 7; j++)
                {
                    bayNodes[j] = parts[j + 1];
                }
                nodeMap[nodeId] = bayNodes;
            }
            return nodeMap;
        }

        // 从CSV读取阻尼器属性
        static List<DamperProperty> LoadDamperProperties(string filePath)
        {
            var properties = new List<DamperProperty>();
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(','); // 逗号分隔
                properties.Add(new DamperProperty
                {
                    Name = parts[0],
                    Ke = double.Parse(parts[1]),
                    Ce = double.Parse(parts[2]),
                    K = double.Parse(parts[3]),
                    C = double.Parse(parts[4])
                });
            }
            return properties;
        }

        // 从CSV读取工况名
        static List<string> LoadRunCaseFlags(string filePath)
        {
            var runCaseFlags = new List<string>();
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(','); // 逗号分隔
                runCaseFlags.Add(parts[1]);
            }
            return runCaseFlags;
        }

        // 随机生成阻尼器配置（保留，暂时注释）
        static DamperConfig GenerateRandomConfig(string id, int maxPropIndex)
        {
            int[] damperState = new int[4];
            int[] cAlphaArrange = new int[4];
            for (int i = 0; i < 4; i++)
            {
                damperState[i] = rand.Next(0, 4); // 每层0-3个阻尼器
                cAlphaArrange[i] = damperState[i] == 0 ? 0 : rand.Next(1, maxPropIndex + 1); // 如果DamperState为0，CAlphaArrange也为0
            }
            return new DamperConfig { ID = id, DamperState = damperState, CAlphaArrange = cAlphaArrange };
        }
    }

    class DamperProperty
    {
        public string Name { get; set; }
        public double Ke { get; set; }
        public double Ce { get; set; }
        public double K { get; set; }
        public double C { get; set; }
    }

    class DamperConfig
    {
        public string ID { get; set; }
        public int[] DamperState { get; set; }
        public int[] CAlphaArrange { get; set; }
    }

    class DisplacementData
    {
        public string LoadCase { get; set; }
        public double StepNum { get; set; }
        public double U1 { get; set; }
        public double U2 { get; set; }
        public double U3 { get; set; }
        public double R1 { get; set; }
        public double R2 { get; set; }
        public double R3 { get; set; }
    }
}