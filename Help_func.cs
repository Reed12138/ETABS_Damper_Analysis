//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace ETABS_Damper_Analysis
//{
//    internal class Help_func
//    {
//        /// <summary>
//        /// 提取每层楼（STORY1-4）在Y方向的层间位移角绝对最大值。
//        /// </summary>
//        /// <param name="filePath">CSV文件路径</param>
//        /// <returns>长度为4的数组，分别表示STORY1到STORY4的Y方向最大漂移值</returns>
//        public static double[] GetMaxDriftY(string filePath)
//        {
//            if (!File.Exists(filePath))
//            {
//                throw new FileNotFoundException("指定的CSV文件不存在。", filePath);
//            }

//            var maxDrifts = new double[4]; // 初始化4个值，分别对应STORY1到STORY4
//            var storyIndices = new[] { "STORY1", "STORY2", "STORY3", "STORY4" };

//            string[] lines = File.ReadAllLines(filePath);
//            if (lines.Length <= 1) // 跳过标题行，检查是否有数据
//            {
//                throw new InvalidDataException("CSV文件没有有效数据。");
//            }

//            // 遍历数据行
//            for (int i = 1; i < lines.Length; i++) // 从1开始跳过标题行
//            {
//                string[] parts = lines[i].Split(',');
//                if (parts.Length != 10) continue; // 确保行格式正确

//                string story = parts[0].Trim();
//                string direction = parts[4].Trim();
//                if (direction != "Y") continue; // 只处理Y方向

//                // 找到楼层索引（0-3对应STORY1-4）
//                int storyIndex = Array.IndexOf(storyIndices, story);
//                if (storyIndex >= 0 && storyIndex < 4) // 只处理STORY1-4
//                {
//                    double drift = double.Parse(parts[5].Trim());
//                    maxDrifts[storyIndex] = Math.Max(maxDrifts[storyIndex], Math.Abs(drift));
//                }
//            }

//            return maxDrifts;
//        }

//        // 示例用法（可选，测试函数）
//        public static void Main()
//        {
//            try
//            {
//                string filePath = "E:\\Wenyi12138\\0306_night_C#\\Output\\results_0_120111.csv"; // 替换为实际文件路径
//                double[] maxDrifts = GetMaxDriftY(filePath);

//                Console.WriteLine("每层Y方向层间位移角绝对最大值：");
//                for (int i = 0; i < maxDrifts.Length; i++)
//                {
//                    Console.WriteLine($"STORY{i + 1}: {maxDrifts[i]}");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"错误: {ex.Message}");
//            }
//        }
//    }
//}
