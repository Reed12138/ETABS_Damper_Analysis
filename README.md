# ETABS Damper Analysis

本项目用于在 ETABS 模型中批量分析不同阻尼器配置下的结构响应（主要是层间位移角），以支持优化和性能评估。

## 📌 项目功能

- 自动初始化 ETABS 软件并打开指定模型
- 读取外部阻尼器参数、节点信息和工况配置
- 支持手动或自动部署阻尼器
- 批量运行多种地震工况下的结构分析
- 提取节点位移，计算并保存：
  - 最大层间位移角（Max Drift）
  - 平均最大层间位移角（Average Drift）
- 结果输出为 CSV 文件，适用于后续处理或可视化

## 🗂 文件结构（示例）

ETABS_Damper_Analysis/
├── bin/ # 编译输出（已被忽略）
├── obj/ # 编译缓存（已被忽略）
├── Properties/ # 项目信息
├── App.config # 配置文件
├── ETABS_Damper_Analysis.csproj # 项目文件
├── Help_func.cs # 辅助函数
├── Program.cs # 主程序入口
├── .gitignore # Git 忽略配置
├── README.md # 项目说明文件 ← 你正在看这个


## 🛠 外部依赖

- [ETABS API](https://www.csiamerica.com/products/etabs)（需本地安装 ETABS 并注册 COM 接口）
- 相关数据文件（请自行准备）：
  - `模型.edb`：ETABS 模型文件
  - `dampers.csv`：阻尼器参数配置表
  - `nodes.csv`：阻尼器部署所需节点编号
  - `runcaseflag_validation.csv`：工况名称清单

## 🚀 使用步骤（概览）

1. 在代码中配置好模型路径和输出路径
2. 启动程序，自动执行以下操作：
   - 初始化 ETABS，打开模型
   - 读取阻尼器配置和节点信息
   - 清空并部署阻尼器
   - 遍历指定工况，运行分析
   - 提取并保存结构响应数据

## 📤 输出结果

结果将保存在如下两个文件中（CSV 格式）：

- `results_driftmax.csv`: 每个工况的最大层间位移角
- `results_driftaverage.csv`: 每个工况的平均最大层间位移角

格式如下：
GM_name,damper_state,C_alpha_arrange,Output
RS1,[3,1,0,1],[1,6,0,2],0.00195


## ✏️ 开发备注

- 当前版本使用**手动输入**的阻尼器配置（`manualDamperState` 与 `manualCAlphaArrange`）
- 可扩展为读取随机或优化算法生成的配置

---