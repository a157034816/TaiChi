using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using Xunit;

namespace TaiChi.Office.Test;

public class ExcelConverterTests
{
    private readonly string _testFilesDir;

    public ExcelConverterTests()
    {
        // 获取测试文件目录
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;
        _testFilesDir = Path.Combine(assemblyDir, "TestFiles");

        // 确保测试文件目录存在
        if (!Directory.Exists(_testFilesDir))
        {
            Directory.CreateDirectory(_testFilesDir);
        }
    }

    #region Excel 转 DataTable 测试

    [Fact]
    public void ConvertToDataTable_FromFile_ValidFile_ReturnsCorrectDataTable()
    {
        // 准备测试数据 - 创建临时Excel文件
        string testFilePath = Path.Combine(_testFilesDir, "testExcel.xlsx");
        PrepareTestExcelFile(testFilePath);

        try
        {
            // 执行
            var converter = new ExcelConverter();
            DataTable result = converter.ConvertToDataTable(testFilePath);

            // 验证
            Assert.NotNull(result);
            Assert.Equal(3, result.Rows.Count);
            Assert.Equal(3, result.Columns.Count);
            Assert.Equal("姓名", result.Columns[0].ColumnName);
            Assert.Equal("年龄", result.Columns[1].ColumnName);
            Assert.Equal("生日", result.Columns[2].ColumnName);

            // 验证第一行数据
            Assert.Equal("张三", result.Rows[0][0]);
            Assert.Equal(25.0, Convert.ToDouble(result.Rows[0][1]));
            Assert.Equal(DateTime.Parse("1998-01-01"), Convert.ToDateTime(result.Rows[0][2]));
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    [Fact]
    public void ConvertToDataTable_WithSpecificSheetName_ReturnsCorrectDataTable()
    {
        // 准备测试数据 - 创建包含多个工作表的Excel文件
        string testFilePath = Path.Combine(_testFilesDir, "multiSheetTest.xlsx");
        PrepareMultiSheetExcelFile(testFilePath);

        try
        {
            // 执行 - 指定读取第二个工作表
            var converter = new ExcelConverter();
            DataTable result = converter.ConvertToDataTable(testFilePath, "Sheet2");

            // 验证
            Assert.NotNull(result);
            Assert.Equal(2, result.Rows.Count);
            Assert.Equal("产品名称", result.Columns[0].ColumnName);
            Assert.Equal("价格", result.Columns[1].ColumnName);

            // 验证数据
            Assert.Equal("产品A", result.Rows[0][0]);
            Assert.Equal(99.5, Convert.ToDouble(result.Rows[0][1]));
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    [Fact]
    public void ConvertToDataTable_WithCustomHeaderRow_ReturnsCorrectDataTable()
    {
        // 准备测试数据 - 创建Excel文件，表头不在第一行
        string testFilePath = Path.Combine(_testFilesDir, "customHeaderTest.xlsx");
        PrepareCustomHeaderExcelFile(testFilePath);

        try
        {
            // 执行 - 指定表头在第2行(索引为1)
            var converter = new ExcelConverter();
            DataTable result = converter.ConvertToDataTable(testFilePath, headerRowIndex: 1);

            // 验证
            Assert.NotNull(result);
            Assert.Equal(2, result.Rows.Count);
            Assert.Equal("商品编号", result.Columns[0].ColumnName);
            Assert.Equal("商品描述", result.Columns[1].ColumnName);

            // 验证数据
            Assert.Equal("A001", result.Rows[0][0]);
            Assert.Equal("测试商品1", result.Rows[0][1]);
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    [Fact]
    public void ConvertToDataTable_WithFieldTransformer_TransformsCorrectly()
    {
        // 准备测试数据
        string testFilePath = Path.Combine(_testFilesDir, "transformTest.xlsx");
        PrepareTestExcelFile(testFilePath);

        try
        {
            // 创建字段转换器
            var fieldTransformer = new DictionaryFieldTransformer();
            fieldTransformer.RegisterMapping("姓名", "Name", typeof(string));
            fieldTransformer.RegisterMapping("年龄", "Age", typeof(int));
            fieldTransformer.RegisterMapping("生日", "Birthday", typeof(DateTime));

            // 执行
            var converter = new ExcelConverter { FieldTransformer = fieldTransformer };
            DataTable result = converter.ConvertToDataTable(testFilePath);

            // 验证
            Assert.NotNull(result);
            Assert.Equal(3, result.Columns.Count);
            Assert.Equal("Name", result.Columns[0].ColumnName);
            Assert.Equal("Age", result.Columns[1].ColumnName);
            Assert.Equal("Birthday", result.Columns[2].ColumnName);

            // 验证列类型
            Assert.Equal(typeof(string), result.Columns[0].DataType);
            Assert.Equal(typeof(int), result.Columns[1].DataType);
            Assert.Equal(typeof(DateTime), result.Columns[2].DataType);

            // 验证数据
            Assert.Equal("张三", result.Rows[0][0]);
            Assert.Equal(25, result.Rows[0][1]);
            Assert.Equal(DateTime.Parse("1998-01-01"), result.Rows[0][2]);
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    [Fact]
    public void ConvertToDataTable_InvalidFile_ThrowsException()
    {
        // 准备 - 不存在的文件路径
        string nonExistentFilePath = Path.Combine(_testFilesDir, "nonexistent.xlsx");

        // 执行 & 验证
        var converter = new ExcelConverter();
        Assert.Throws<FileNotFoundException>(() => converter.ConvertToDataTable(nonExistentFilePath));
    }

    [Fact]
    public void ConvertToDataTable_UnsupportedFileFormat_ThrowsException()
    {
        // 准备 - 创建非Excel格式文件
        string testFilePath = Path.Combine(_testFilesDir, "test.txt");
        File.WriteAllText(testFilePath, "This is not an Excel file");

        try
        {
            // 执行 & 验证
            var converter = new ExcelConverter();
            Assert.Throws<ArgumentException>(() => converter.ConvertToDataTable(testFilePath));
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    [Fact]
    public void ConvertToDataTable_FromStream_ValidStream_ReturnsCorrectDataTable()
    {
        // 准备测试数据
        string testFilePath = Path.Combine(_testFilesDir, "streamTest.xlsx");
        PrepareTestExcelFile(testFilePath);

        try
        {
            // 使用流读取
            using var stream = new FileStream(testFilePath, FileMode.Open, FileAccess.Read);

            // 执行
            var converter = new ExcelConverter();
            DataTable result = converter.ConvertToDataTable(stream, ".xlsx");

            // 验证
            Assert.NotNull(result);
            Assert.Equal(3, result.Rows.Count);
            Assert.Equal(3, result.Columns.Count);
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    #endregion

    #region DataTable 转 Excel 测试

    [Fact]
    public void ConvertToExcel_ValidDataTable_CreatesExcelFile()
    {
        // 准备测试数据
        DataTable dataTable = CreateTestDataTable();
        string outputPath = Path.Combine(_testFilesDir, "output.xlsx");

        try
        {
            // 执行
            var converter = new ExcelConverter();
            converter.ConvertToExcel(dataTable, outputPath);

            // 验证文件是否创建
            Assert.True(File.Exists(outputPath));

            // 验证生成的Excel内容
            using (var stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook = new XSSFWorkbook(stream);
                ISheet sheet = workbook.GetSheetAt(0);

                // 验证表头
                IRow headerRow = sheet.GetRow(0);
                Assert.Equal("Name", headerRow.GetCell(0).StringCellValue);
                Assert.Equal("Age", headerRow.GetCell(1).StringCellValue);
                Assert.Equal("BirthDate", headerRow.GetCell(2).StringCellValue);

                // 验证第一行数据
                IRow dataRow = sheet.GetRow(1);
                Assert.Equal("张三", dataRow.GetCell(0).StringCellValue);
                Assert.Equal(25, dataRow.GetCell(1).NumericCellValue);

                // 关闭工作簿
                workbook.Close();
            }
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void ConvertToExcel_WithSheetName_CreatesExcelFileWithCustomSheetName()
    {
        // 准备测试数据
        DataTable dataTable = CreateTestDataTable();
        string outputPath = Path.Combine(_testFilesDir, "customSheet.xlsx");
        string customSheetName = "用户数据";

        try
        {
            // 执行
            var converter = new ExcelConverter();
            converter.ConvertToExcel(dataTable, outputPath, customSheetName);

            // 验证文件是否创建
            Assert.True(File.Exists(outputPath));

            // 验证工作表名称
            using (var stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook = new XSSFWorkbook(stream);
                Assert.Equal(customSheetName, workbook.GetSheetAt(0).SheetName);
                workbook.Close();
            }
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void ConvertToExcel_WithFieldTransformer_TransformsColumnNames()
    {
        // 准备测试数据
        DataTable dataTable = CreateTestDataTable();
        string outputPath = Path.Combine(_testFilesDir, "transformed.xlsx");

        // 创建字段转换器
        var fieldTransformer = new DictionaryFieldTransformer();
        fieldTransformer.RegisterMapping("姓名", "Name", typeof(string));
        fieldTransformer.RegisterMapping("年龄", "Age", typeof(int));
        fieldTransformer.RegisterMapping("出生日期", "BirthDate", typeof(DateTime));

        try
        {
            // 执行
            var converter = new ExcelConverter { FieldTransformer = fieldTransformer };
            converter.ConvertToExcel(dataTable, outputPath);

            // 验证生成的Excel内容
            using (var stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook = new XSSFWorkbook(stream);
                ISheet sheet = workbook.GetSheetAt(0);

                // 验证表头被转换
                IRow headerRow = sheet.GetRow(0);
                Assert.Equal("姓名", headerRow.GetCell(0).StringCellValue);
                Assert.Equal("年龄", headerRow.GetCell(1).StringCellValue);
                Assert.Equal("出生日期", headerRow.GetCell(2).StringCellValue);

                workbook.Close();
            }
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void ConvertToExcelStream_ValidDataTable_ReturnsMemoryStream()
    {
        // 准备测试数据
        DataTable dataTable = CreateTestDataTable();

        // 执行
        var converter = new ExcelConverter();
        using (MemoryStream memoryStream = converter.ConvertToExcelStream(dataTable))
        {
            // 验证
            Assert.NotNull(memoryStream);
            Assert.True(memoryStream.Length > 0);

            // 从内存流读取Excel并验证内容
            memoryStream.Position = 0;
            IWorkbook workbook = new XSSFWorkbook(memoryStream);
            ISheet sheet = workbook.GetSheetAt(0);

            // 验证表头
            IRow headerRow = sheet.GetRow(0);
            Assert.Equal("Name", headerRow.GetCell(0).StringCellValue);

            // 验证数据行
            IRow dataRow = sheet.GetRow(1);
            Assert.Equal("张三", dataRow.GetCell(0).StringCellValue);

            workbook.Close();
        }
    }

    [Fact]
    public void ConvertToExcel_MultipleDataTables_CreatesMultiSheetExcel()
    {
        // 准备测试数据 - 两个不同的数据表
        DataTable table1 = CreateTestDataTable();

        DataTable table2 = new DataTable();
        table2.Columns.Add("ProductId", typeof(string));
        table2.Columns.Add("ProductName", typeof(string));
        table2.Columns.Add("Price", typeof(decimal));
        table2.Rows.Add("P001", "产品1", 88.5m);
        table2.Rows.Add("P002", "产品2", 99.9m);

        string outputPath = Path.Combine(_testFilesDir, "multisheet.xlsx");

        try
        {
            // 执行
            var converter = new ExcelConverter();
            converter.ConvertToExcel(
                new DataTable[] { table1, table2 },
                outputPath,
                new string[] { "用户数据", "产品数据" });

            // 验证文件是否创建
            Assert.True(File.Exists(outputPath));

            // 验证生成的Excel内容
            using (var stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook = new XSSFWorkbook(stream);

                // 验证工作表数量和名称
                Assert.Equal(2, workbook.NumberOfSheets);
                Assert.Equal("用户数据", workbook.GetSheetAt(0).SheetName);
                Assert.Equal("产品数据", workbook.GetSheetAt(1).SheetName);

                // 验证第一个工作表内容
                ISheet sheet1 = workbook.GetSheetAt(0);
                Assert.Equal("Name", sheet1.GetRow(0).GetCell(0).StringCellValue);

                // 验证第二个工作表内容
                ISheet sheet2 = workbook.GetSheetAt(1);
                Assert.Equal("ProductId", sheet2.GetRow(0).GetCell(0).StringCellValue);
                Assert.Equal("P001", sheet2.GetRow(1).GetCell(0).StringCellValue);

                workbook.Close();
            }
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 创建用于测试的DataTable
    /// </summary>
    private DataTable CreateTestDataTable()
    {
        DataTable dataTable = new DataTable();
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Age", typeof(int));
        dataTable.Columns.Add("BirthDate", typeof(DateTime));

        dataTable.Rows.Add("张三", 25, new DateTime(1998, 1, 1));
        dataTable.Rows.Add("李四", 30, new DateTime(1993, 5, 15));
        dataTable.Rows.Add("王五", 22, new DateTime(2001, 10, 8));

        return dataTable;
    }

    /// <summary>
    /// 准备测试用的Excel文件
    /// </summary>
    private void PrepareTestExcelFile(string filePath)
    {
        IWorkbook workbook = new XSSFWorkbook();
        ISheet sheet = workbook.CreateSheet("Sheet1");

        // 创建表头
        IRow headerRow = sheet.CreateRow(0);
        headerRow.CreateCell(0).SetCellValue("姓名");
        headerRow.CreateCell(1).SetCellValue("年龄");
        headerRow.CreateCell(2).SetCellValue("生日");

        // 创建数据行
        IRow dataRow1 = sheet.CreateRow(1);
        dataRow1.CreateCell(0).SetCellValue("张三");
        dataRow1.CreateCell(1).SetCellValue(25);

        ICellStyle dateStyle = workbook.CreateCellStyle();
        IDataFormat dataFormat = workbook.CreateDataFormat();
        dateStyle.DataFormat = dataFormat.GetFormat("yyyy-mm-dd");

        ICell dateCell = dataRow1.CreateCell(2);
        dateCell.SetCellValue(new DateTime(1998, 1, 1));
        dateCell.CellStyle = dateStyle;

        IRow dataRow2 = sheet.CreateRow(2);
        dataRow2.CreateCell(0).SetCellValue("李四");
        dataRow2.CreateCell(1).SetCellValue(30);
        dateCell = dataRow2.CreateCell(2);
        dateCell.SetCellValue(new DateTime(1993, 5, 15));
        dateCell.CellStyle = dateStyle;

        IRow dataRow3 = sheet.CreateRow(3);
        dataRow3.CreateCell(0).SetCellValue("王五");
        dataRow3.CreateCell(1).SetCellValue(22);
        dateCell = dataRow3.CreateCell(2);
        dateCell.SetCellValue(new DateTime(2001, 10, 8));
        dateCell.CellStyle = dateStyle;

        // 保存文件
        using var fileStream = new FileStream(filePath, FileMode.Create);
        workbook.Write(fileStream);
        workbook.Close();
    }

    /// <summary>
    /// 准备包含多个工作表的Excel文件
    /// </summary>
    private void PrepareMultiSheetExcelFile(string filePath)
    {
        IWorkbook workbook = new XSSFWorkbook();

        // 第一个工作表 - 用户数据
        ISheet sheet1 = workbook.CreateSheet("Sheet1");

        IRow sheet1HeaderRow = sheet1.CreateRow(0);
        sheet1HeaderRow.CreateCell(0).SetCellValue("姓名");
        sheet1HeaderRow.CreateCell(1).SetCellValue("年龄");

        IRow sheet1DataRow1 = sheet1.CreateRow(1);
        sheet1DataRow1.CreateCell(0).SetCellValue("张三");
        sheet1DataRow1.CreateCell(1).SetCellValue(25);

        // 第二个工作表 - 产品数据
        ISheet sheet2 = workbook.CreateSheet("Sheet2");

        IRow sheet2HeaderRow = sheet2.CreateRow(0);
        sheet2HeaderRow.CreateCell(0).SetCellValue("产品名称");
        sheet2HeaderRow.CreateCell(1).SetCellValue("价格");

        IRow sheet2DataRow1 = sheet2.CreateRow(1);
        sheet2DataRow1.CreateCell(0).SetCellValue("产品A");
        sheet2DataRow1.CreateCell(1).SetCellValue(99.5);

        IRow sheet2DataRow2 = sheet2.CreateRow(2);
        sheet2DataRow2.CreateCell(0).SetCellValue("产品B");
        sheet2DataRow2.CreateCell(1).SetCellValue(199.9);

        // 保存文件
        using var fileStream = new FileStream(filePath, FileMode.Create);
        workbook.Write(fileStream);
        workbook.Close();
    }

    /// <summary>
    /// 准备表头不在第一行的Excel文件
    /// </summary>
    private void PrepareCustomHeaderExcelFile(string filePath)
    {
        IWorkbook workbook = new XSSFWorkbook();
        ISheet sheet = workbook.CreateSheet("Sheet1");

        // 第一行是说明文字，不是表头
        IRow infoRow = sheet.CreateRow(0);
        infoRow.CreateCell(0).SetCellValue("这是一个测试文件");

        // 第二行是表头
        IRow headerRow = sheet.CreateRow(1);
        headerRow.CreateCell(0).SetCellValue("商品编号");
        headerRow.CreateCell(1).SetCellValue("商品描述");

        // 数据行
        IRow dataRow1 = sheet.CreateRow(2);
        dataRow1.CreateCell(0).SetCellValue("A001");
        dataRow1.CreateCell(1).SetCellValue("测试商品1");

        IRow dataRow2 = sheet.CreateRow(3);
        dataRow2.CreateCell(0).SetCellValue("A002");
        dataRow2.CreateCell(1).SetCellValue("测试商品2");

        // 保存文件
        using var fileStream = new FileStream(filePath, FileMode.Create);
        workbook.Write(fileStream);
        workbook.Close();
    }

    #endregion
}