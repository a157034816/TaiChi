using System.Data;
using TaiChi.Office;

// ExcelConverter示例程序
// 这个程序展示了如何使用ExcelConverter类进行Excel与DataTable之间的转换

// 创建样例数据表
DataTable CreateSampleDataTable()
{
    DataTable dataTable = new DataTable("产品信息");
    
    // 添加列
    dataTable.Columns.Add("产品编号", typeof(int));
    dataTable.Columns.Add("产品名称", typeof(string));
    dataTable.Columns.Add("价格", typeof(decimal));
    dataTable.Columns.Add("生产日期", typeof(DateTime));
    dataTable.Columns.Add("库存", typeof(int));
    dataTable.Columns.Add("是否在售", typeof(bool));
    
    // 添加行数据
    dataTable.Rows.Add(1001, "笔记本电脑", 5999.99, DateTime.Now.AddDays(-30), 50, true);
    dataTable.Rows.Add(1002, "智能手机", 3999.00, DateTime.Now.AddDays(-15), 100, true);
    dataTable.Rows.Add(1003, "无线耳机", 899.00, DateTime.Now.AddDays(-60), 20, false);
    dataTable.Rows.Add(1004, "机械键盘", 599.00, DateTime.Now.AddDays(-45), 35, true);
    dataTable.Rows.Add(1005, "显示器", 1899.00, DateTime.Now.AddDays(-10), 15, true);
    
    return dataTable;
}

// 示例1：将DataTable导出为Excel文件
void ExportDataTableToExcel()
{
    Console.WriteLine("示例1：将DataTable导出为Excel文件");
    
    DataTable productTable = CreateSampleDataTable();
    string excelFilePath = "产品信息.xlsx";
    
    ExcelConverter converter = new ExcelConverter();
    converter.ConvertToExcel(productTable, excelFilePath, "产品列表");
    
    Console.WriteLine($"已成功将数据导出到Excel文件：{Path.GetFullPath(excelFilePath)}");
    Console.WriteLine();
}

// 示例2：将多个DataTable导出为多个工作表
void ExportMultipleDataTablesToExcel()
{
    Console.WriteLine("示例2：将多个DataTable导出为多个工作表");
    
    // 创建第一个数据表
    DataTable productTable = CreateSampleDataTable();
    
    // 创建第二个数据表（员工信息）
    DataTable employeeTable = new DataTable("员工信息");
    employeeTable.Columns.Add("工号", typeof(int));
    employeeTable.Columns.Add("姓名", typeof(string));
    employeeTable.Columns.Add("部门", typeof(string));
    employeeTable.Columns.Add("入职日期", typeof(DateTime));
    employeeTable.Columns.Add("薪资", typeof(decimal));
    
    employeeTable.Rows.Add(1, "张三", "销售部", DateTime.Now.AddYears(-2), 10000);
    employeeTable.Rows.Add(2, "李四", "技术部", DateTime.Now.AddYears(-1), 15000);
    employeeTable.Rows.Add(3, "王五", "人事部", DateTime.Now.AddMonths(-6), 8000);
    
    // 导出多个DataTable
    string multiSheetFilePath = "公司信息.xlsx";
    ExcelConverter converter = new ExcelConverter();
    converter.ConvertToExcel(
        new DataTable[] { productTable, employeeTable },
        multiSheetFilePath,
        new string[] { "产品列表", "员工列表" });
    
    Console.WriteLine($"已成功将多个数据表导出到Excel文件：{Path.GetFullPath(multiSheetFilePath)}");
    Console.WriteLine();
}

// 示例3：从Excel文件导入DataTable
void ImportExcelToDataTable()
{
    Console.WriteLine("示例3：从Excel文件导入DataTable");
    
    // 首先确保Excel文件存在
    string excelFilePath = "产品信息.xlsx";
    if (!File.Exists(excelFilePath))
    {
        DataTable productTable = CreateSampleDataTable();
        ExcelConverter converter = new ExcelConverter();
        converter.ConvertToExcel(productTable, excelFilePath, "产品列表");
    }
    
    // 从Excel文件导入DataTable
    ExcelConverter importConverter = new ExcelConverter();
    
    // 自定义字段名称转换器示例（可选）
    importConverter.FieldNameTransformer = (originalName) =>
    {
        // 将字段名中的空格替换为下划线
        return originalName.Replace(" ", "_");
    };
    
    DataTable importedTable = importConverter.ConvertToDataTable(excelFilePath, "产品列表");
    
    // 显示导入的数据
    Console.WriteLine("从Excel导入的数据:");
    Console.WriteLine($"表名: {importedTable.TableName}");
    Console.WriteLine($"列数: {importedTable.Columns.Count}, 行数: {importedTable.Rows.Count}");
    
    // 显示列名
    Console.WriteLine("\n列名:");
    foreach (DataColumn column in importedTable.Columns)
    {
        Console.Write($"{column.ColumnName}\t");
    }
    Console.WriteLine();
    
    // 显示前3行数据
    Console.WriteLine("\n数据(前3行):");
    int rowCount = Math.Min(importedTable.Rows.Count, 3);
    for (int i = 0; i < rowCount; i++)
    {
        DataRow row = importedTable.Rows[i];
        foreach (var item in row.ItemArray)
        {
            Console.Write($"{item}\t");
        }
        Console.WriteLine();
    }
    
    Console.WriteLine();
}

// 示例4：从流中读取Excel数据
void ImportExcelFromStream()
{
    Console.WriteLine("示例4：从流中读取Excel数据");
    
    // 首先创建一个示例Excel文件
    DataTable sampleTable = CreateSampleDataTable();
    ExcelConverter converter = new ExcelConverter();
    
    // 创建内存流并写入Excel数据
    using MemoryStream memoryStream = converter.ConvertToExcelStream(sampleTable, "内存流示例");
    
    // 从流中读取Excel数据
    ExcelConverter streamImporter = new ExcelConverter();
    DataTable importedFromStream = streamImporter.ConvertToDataTable(
        memoryStream, 
        ".xlsx",  // 指定文件扩展名
        "内存流示例"  // 指定工作表名称
    );
    
    // 显示导入数据的基本信息
    Console.WriteLine("从内存流中导入的数据基本信息:");
    Console.WriteLine($"列数: {importedFromStream.Columns.Count}, 行数: {importedFromStream.Rows.Count}");
    Console.WriteLine();
}

try
{
    // 运行所有示例
    ExportDataTableToExcel();
    ExportMultipleDataTablesToExcel();
    ImportExcelToDataTable();
    ImportExcelFromStream();
    
    Console.WriteLine("所有示例运行完成，按任意键退出...");
}
catch (Exception ex)
{
    Console.WriteLine($"示例执行过程中发生错误: {ex.Message}");
    Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
}

Console.ReadKey();
