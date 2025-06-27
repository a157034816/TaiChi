using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace TaiChi.Office;

/// <summary>
/// Excel转换器类,支持将Excel表格转换为DataTable以及将DataTable转换为Excel
/// </summary>
public class ExcelConverter
{
    /// <summary>
    /// 支持的Excel文件扩展名
    /// </summary>
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls"
    };

    /// <summary>
    /// 字段名称转换委托,用于自定义列名转换规则
    /// </summary>
    /// <param name="originalName">原始列名</param>
    /// <returns>转换后的列名</returns>
    public delegate string FieldNameConverter(string originalName);

    /// <summary>
    /// 字段名称转换器，用于自定义列名转换规则
    /// </summary>
    [Obsolete("请使用 FieldTransformer 属性替代，该属性支持更丰富的字段转换功能")]
    public FieldNameConverter? FieldNameTransformer { get; set; }

    /// <summary>
    /// 字段转换器，用于Excel与DataTable之间的字段名称和数据类型转换
    /// </summary>
    public IFieldTransformer? FieldTransformer { get; set; }

    /// <summary>
    /// 将Excel文件转换为DataTable
    /// </summary>
    /// <param name="filePath">Excel文件路径</param>
    /// <param name="sheetName">工作表名称,为空则使用第一个工作表</param>
    /// <param name="headerRowIndex">表头行索引,默认为0</param>
    /// <param name="startColumnIndex">起始列索引,默认为0</param>
    /// <returns>转换后的DataTable</returns>
    /// <exception cref="ArgumentNullException">文件路径为空时抛出</exception>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    /// <exception cref="ArgumentException">文件格式不受支持时抛出</exception>
    /// <exception cref="InvalidOperationException">处理Excel文件时出错时抛出</exception>
    public DataTable ConvertToDataTable(
        string filePath,
        string? sheetName = null,
        int headerRowIndex = 0,
        int startColumnIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("找不到指定的Excel文件", filePath);
        }

        string extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new ArgumentException($"不支持的文件格式: {extension}，仅支持.xlsx和.xls格式", nameof(filePath));
        }

        if (headerRowIndex < 0)
        {
            throw new ArgumentException("表头行索引不能小于0", nameof(headerRowIndex));
        }

        if (startColumnIndex < 0)
        {
            throw new ArgumentException("起始列索引不能小于0", nameof(startColumnIndex));
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return ConvertToDataTable(stream, extension, sheetName, headerRowIndex, startColumnIndex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"读取Excel文件时发生IO错误: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"处理Excel文件时发生错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 将Excel流转换为DataTable
    /// </summary>
    /// <param name="stream">Excel文件流</param>
    /// <param name="fileExtension">文件扩展名(.xls或.xlsx)</param>
    /// <param name="sheetName">工作表名称,为空则使用第一个工作表</param>
    /// <param name="headerRowIndex">表头行索引,默认为0</param>
    /// <param name="startColumnIndex">起始列索引,默认为0</param>
    /// <returns>转换后的DataTable</returns>
    /// <exception cref="ArgumentNullException">流为空时抛出</exception>
    /// <exception cref="ArgumentException">扩展名不受支持或其他参数错误时抛出</exception>
    /// <exception cref="InvalidOperationException">处理Excel流时出错时抛出</exception>
    public DataTable ConvertToDataTable(
        Stream stream,
        string fileExtension,
        string? sheetName = null,
        int headerRowIndex = 0,
        int startColumnIndex = 0)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream), "Excel流不能为空");
        }

        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            throw new ArgumentException("文件扩展名不能为空", nameof(fileExtension));
        }

        if (!SupportedExtensions.Contains(fileExtension))
        {
            throw new ArgumentException($"不支持的文件格式: {fileExtension}，仅支持.xlsx和.xls格式", nameof(fileExtension));
        }

        if (headerRowIndex < 0)
        {
            throw new ArgumentException("表头行索引不能小于0", nameof(headerRowIndex));
        }

        if (startColumnIndex < 0)
        {
            throw new ArgumentException("起始列索引不能小于0", nameof(startColumnIndex));
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException("流不可读", nameof(stream));
        }

        try
        {
            IWorkbook workbook = CreateWorkbook(stream, fileExtension);
            try
            {
                if (workbook.NumberOfSheets == 0)
                {
                    throw new InvalidOperationException("Excel文件不包含任何工作表");
                }

                ISheet sheet = GetSheet(workbook, sheetName);
                return ProcessSheet(sheet, headerRowIndex, startColumnIndex);
            }
            finally
            {
                // 确保工作簿资源被释放
                workbook.Close();
            }
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"读取Excel流时发生IO错误: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"处理Excel流时发生错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 将Excel的工作表转换为DataTable
    /// </summary>
    /// <param name="sheet">NPOI工作表</param>
    /// <param name="headerRowIndex">表头行索引</param>
    /// <param name="startColumnIndex">起始列索引</param>
    /// <returns>转换后的DataTable</returns>
    /// <exception cref="ArgumentNullException">工作表为空时抛出</exception>
    /// <exception cref="ArgumentException">参数错误时抛出</exception>
    /// <exception cref="InvalidOperationException">处理工作表时出错时抛出</exception>
    public DataTable ConvertToDataTable(
        ISheet sheet,
        int headerRowIndex = 0,
        int startColumnIndex = 0)
    {
        if (sheet == null)
        {
            throw new ArgumentNullException(nameof(sheet), "工作表不能为空");
        }

        if (headerRowIndex < 0)
        {
            throw new ArgumentException("表头行索引不能小于0", nameof(headerRowIndex));
        }

        if (startColumnIndex < 0)
        {
            throw new ArgumentException("起始列索引不能小于0", nameof(startColumnIndex));
        }

        try
        {
            return ProcessSheet(sheet, headerRowIndex, startColumnIndex);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"处理工作表时发生错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 创建Excel工作簿对象
    /// </summary>
    /// <param name="stream">Excel文件流</param>
    /// <param name="fileExtension">文件扩展名(.xls或.xlsx)</param>
    /// <returns>创建的工作簿对象</returns>
    /// <exception cref="ArgumentNullException">流或扩展名为空时抛出</exception>
    /// <exception cref="ArgumentException">扩展名格式错误时抛出</exception>
    /// <exception cref="InvalidOperationException">创建工作簿对象失败时抛出</exception>
    private IWorkbook CreateWorkbook(Stream stream, string fileExtension)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream), "Excel流不能为空");
        }

        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            throw new ArgumentException("文件扩展名不能为空", nameof(fileExtension));
        }

        try
        {
            return fileExtension.ToLower() == ".xlsx" 
                ? new XSSFWorkbook(stream) 
                : (IWorkbook)new HSSFWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"创建工作簿时出错: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取指定名称的工作表，如果名称为空则获取第一个工作表
    /// </summary>
    /// <param name="workbook">Excel工作簿对象</param>
    /// <param name="sheetName">工作表名称，为空则获取第一个工作表</param>
    /// <returns>获取到的工作表对象</returns>
    /// <exception cref="ArgumentNullException">工作簿对象为空时抛出</exception>
    /// <exception cref="InvalidOperationException">工作簿不包含任何工作表或获取工作表失败时抛出</exception>
    /// <exception cref="ArgumentException">找不到指定名称的工作表时抛出</exception>
    private ISheet GetSheet(IWorkbook workbook, string? sheetName)
    {
        if (workbook == null)
        {
            throw new ArgumentNullException(nameof(workbook), "工作簿不能为空");
        }

        try
        {
            if (string.IsNullOrEmpty(sheetName))
            {
                if (workbook.NumberOfSheets == 0)
                {
                    throw new InvalidOperationException("Excel文件不包含任何工作表");
                }
                return workbook.GetSheetAt(0);
            }
            
            ISheet? sheet = workbook.GetSheet(sheetName);
            if (sheet == null)
            {
                throw new ArgumentException($"找不到名为 '{sheetName}' 的工作表", nameof(sheetName));
            }
            
            return sheet;
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"获取工作表时出错: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 处理Excel工作表并转换为DataTable
    /// </summary>
    /// <param name="sheet">要处理的工作表</param>
    /// <param name="headerRowIndex">表头行索引，从0开始</param>
    /// <param name="startColumnIndex">起始列索引，从0开始</param>
    /// <returns>转换后的DataTable对象</returns>
    /// <exception cref="ArgumentNullException">工作表对象为空时抛出</exception>
    /// <exception cref="ArgumentException">表头行索引或起始列索引无效时抛出</exception>
    /// <exception cref="InvalidOperationException">处理工作表数据失败时抛出</exception>
    /// <remarks>
    /// 该方法会读取工作表中的数据，将表头行转换为DataTable的列，并将数据行转换为DataTable的行。
    /// 如果设置了字段转换器，会应用自定义的列名和数据类型转换规则。
    /// </remarks>
    private DataTable ProcessSheet(
        ISheet sheet, 
        int headerRowIndex, 
        int startColumnIndex)
    {
        if (sheet == null)
        {
            throw new ArgumentNullException(nameof(sheet), "工作表不能为空");
        }

        if (headerRowIndex < 0 || headerRowIndex > sheet.LastRowNum)
        {
            throw new ArgumentException($"表头行索引 {headerRowIndex} 超出范围", nameof(headerRowIndex));
        }

        if (startColumnIndex < 0)
        {
            throw new ArgumentException("起始列索引不能小于0", nameof(startColumnIndex));
        }

        DataTable dataTable = new DataTable();
        IRow? headerRow = sheet.GetRow(headerRowIndex);
        
        if (headerRow == null)
        {
            throw new InvalidOperationException($"在索引 {headerRowIndex} 处找不到表头行");
        }
        
        // 获取最后一列的索引（考虑到起始列索引）
        int lastCellIndex = headerRow.LastCellNum;
        
        if (lastCellIndex <= startColumnIndex)
        {
            // 如果表头行为空或最后单元格索引小于起始列索引，返回空表
            return dataTable;
        }
        
        // 添加列
        for (int i = startColumnIndex; i < lastCellIndex; i++)
        {
            ICell? cell = headerRow.GetCell(i);
            string excelColumnName = cell?.ToString() ?? $"Column{i}";
            
            // 确保列名不为空
            if (string.IsNullOrWhiteSpace(excelColumnName))
            {
                excelColumnName = $"Column{i}";
            }
            
            string columnName = excelColumnName;
            Type columnType = typeof(string); // 默认为字符串类型
            
            // 应用自定义字段转换器
            if (FieldTransformer != null)
            {
                try
                {
                    var fieldInfo = FieldTransformer.TransformExcelToDataTable(excelColumnName);
                    if (!string.IsNullOrWhiteSpace(fieldInfo.Name))
                    {
                        columnName = fieldInfo.Name;
                    }
                    
                    if (fieldInfo.Type != null)
                    {
                        columnType = fieldInfo.Type;
                    }
                }
                catch (Exception ex)
                {
                    // 如果字段转换失败，记录错误但继续使用原始值
                    System.Diagnostics.Debug.WriteLine($"字段转换失败: {ex.Message}");
                }
            }
            // 向下兼容旧的 FieldNameTransformer（已过时）
            else if (FieldNameTransformer != null)
            {
                try
                {
                    string? transformedName = FieldNameTransformer(excelColumnName);
                    if (!string.IsNullOrWhiteSpace(transformedName))
                    {
                        columnName = transformedName;
                    }
                }
                catch (Exception ex)
                {
                    // 如果列名转换失败，记录错误但继续使用原始列名
                    System.Diagnostics.Debug.WriteLine($"列名转换失败: {ex.Message}");
                }
            }
            
            // 确保列名唯一
            columnName = EnsureUniqueColumnName(dataTable, columnName);
            
            // 添加列，使用指定的数据类型
            dataTable.Columns.Add(columnName, columnType);
        }
        
        // 如果没有列，则返回空表
        if (dataTable.Columns.Count == 0)
        {
            return dataTable;
        }
        
        // 处理数据行
        int rowCount = sheet.LastRowNum;
        for (int i = headerRowIndex + 1; i <= rowCount; i++)
        {
            IRow? row = sheet.GetRow(i);
            if (row == null) continue;
            
            DataRow dataRow = dataTable.NewRow();
            bool hasData = false;
            
            for (int j = startColumnIndex; j < lastCellIndex && j - startColumnIndex < dataTable.Columns.Count; j++)
            {
                int columnIndex = j - startColumnIndex;
                if (columnIndex >= dataTable.Columns.Count)
                {
                    break; // 防止访问超出列数
                }
                
                ICell? cell = row.GetCell(j);
                if (cell != null)
                {
                    try
                    {
                        object cellValue = GetCellValue(cell);
                        if (cellValue != null && cellValue != DBNull.Value)
                        {
                            // 尝试将单元格值转换为目标列的数据类型
                            object convertedValue = ConvertValueToTargetType(
                                cellValue, 
                                dataTable.Columns[columnIndex].DataType);
                                
                            dataRow[columnIndex] = convertedValue;
                            hasData = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果单元格值获取失败，记录错误并继续
                        System.Diagnostics.Debug.WriteLine($"获取单元格值失败: {ex.Message}");
                    }
                }
            }
            
            // 只添加有数据的行
            if (hasData)
            {
                dataTable.Rows.Add(dataRow);
            }
        }
        
        return dataTable;
    }

    /// <summary>
    /// 尝试将值转换为目标数据类型
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="targetType">目标数据类型</param>
    /// <returns>转换后的值，如果转换失败则返回该类型的默认值或DBNull</returns>
    /// <remarks>
    /// 该方法支持常见类型之间的转换，包括:
    /// - 字符串 (string)
    /// - 整数 (int/int?)
    /// - 长整数 (long/long?)
    /// - 浮点数 (double/double?)
    /// - 十进制数 (decimal/decimal?)
    /// - 日期时间 (DateTime/DateTime?)
    /// - 布尔值 (bool/bool?)
    /// 
    /// 对于布尔值，字符串"true"、"yes"、"1"、"是"、"真"都会被转换为true。
    /// 如果转换失败，对于值类型会返回该类型的默认值，对于引用类型会返回null或DBNull。
    /// </remarks>
    private object ConvertValueToTargetType(object value, Type targetType)
    {
        if (value == null || value == DBNull.Value)
        {
            return DBNull.Value;
        }
        
        try
        {
            // 如果值已经是目标类型，直接返回
            if (value.GetType() == targetType)
            {
                return value;
            }
            
            // 处理常见类型转换
            if (targetType == typeof(string))
            {
                return value.ToString() ?? string.Empty;
            }
            else if (targetType == typeof(int) || targetType == typeof(int?))
            {
                if (value is double doubleValue)
                {
                    return Convert.ToInt32(doubleValue);
                }
                return Convert.ToInt32(value);
            }
            else if (targetType == typeof(long) || targetType == typeof(long?))
            {
                if (value is double doubleValue)
                {
                    return Convert.ToInt64(doubleValue);
                }
                return Convert.ToInt64(value);
            }
            else if (targetType == typeof(double) || targetType == typeof(double?))
            {
                return Convert.ToDouble(value);
            }
            else if (targetType == typeof(decimal) || targetType == typeof(decimal?))
            {
                if (value is double doubleValue)
                {
                    return Convert.ToDecimal(doubleValue);
                }
                return Convert.ToDecimal(value);
            }
            else if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
            {
                if (value is DateTime)
                {
                    return value;
                }
                return Convert.ToDateTime(value);
            }
            else if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                if (value is string strValue)
                {
                    // 处理常见的布尔值字符串表示
                    return strValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                           strValue.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                           strValue.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                           strValue.Equals("是", StringComparison.OrdinalIgnoreCase) ||
                           strValue.Equals("真", StringComparison.OrdinalIgnoreCase);
                }
                return Convert.ToBoolean(value);
            }
            
            // 尝试使用通用转换方法
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // 如果转换失败，尝试以字符串形式返回原始值
            try
            {
                if (targetType == typeof(string))
                {
                    return value.ToString() ?? string.Empty;
                }
                else
                {
                    // 无法转换，返回该列的默认值
                    return targetType.IsValueType ? Activator.CreateInstance(targetType)! : null!;
                }
            }
            catch
            {
                // 如果仍然失败，返回DBNull
                return DBNull.Value;
            }
        }
    }

    /// <summary>
    /// 确保DataTable列名的唯一性
    /// </summary>
    /// <param name="dataTable">DataTable对象</param>
    /// <param name="columnName">要检查的列名</param>
    /// <returns>确保唯一性后的列名</returns>
    /// <remarks>
    /// 如果指定的列名已经存在于DataTable中，则会在列名后添加递增的数字后缀，
    /// 例如：已存在"Column"，则返回"Column_1"，如果"Column_1"也存在，则返回"Column_2"，以此类推。
    /// </remarks>
    private string EnsureUniqueColumnName(DataTable dataTable, string columnName)
    {
        string uniqueName = columnName;
        int counter = 1;
        
        while (dataTable.Columns.Contains(uniqueName))
        {
            uniqueName = $"{columnName}_{counter++}";
        }
        
        return uniqueName;
    }

    /// <summary>
    /// 获取Excel单元格的值并转换为对应的.NET类型
    /// </summary>
    /// <param name="cell">Excel单元格对象</param>
    /// <returns>转换后的单元格值，如果单元格为空或转换失败则返回DBNull</returns>
    /// <remarks>
    /// 根据单元格的类型进行对应的值提取：
    /// - 数值类型：返回数值或日期（如果是日期格式）
    /// - 字符串类型：返回字符串值
    /// - 布尔类型：返回布尔值
    /// - 公式类型：尝试获取公式计算结果，如果失败则尝试返回公式字符串
    /// - 空白或错误类型：返回DBNull
    /// 
    /// 该方法会捕获并处理单元格值获取过程中可能出现的异常，确保不会因单个单元格的问题影响整体处理。
    /// </remarks>
    private object GetCellValue(ICell cell)
    {
        if (cell == null)
        {
            return DBNull.Value;
        }

        try
        {
            switch (cell.CellType)
            {
                case CellType.Numeric:
                    // 检查是否是日期格式
                    if (DateUtil.IsCellDateFormatted(cell))
                    {
                        try
                        {
                            return cell.DateCellValue;
                        }
                        catch
                        {
                            // 如果日期解析失败，返回原始数值
                            return cell.NumericCellValue;
                        }
                    }
                    return cell.NumericCellValue;
                
                case CellType.String:
                    return string.IsNullOrEmpty(cell.StringCellValue) ? DBNull.Value : cell.StringCellValue;
                
                case CellType.Boolean:
                    return cell.BooleanCellValue;
                
                case CellType.Formula:
                    try
                    {
                        switch (cell.CachedFormulaResultType)
                        {
                            case CellType.Numeric:
                                if (DateUtil.IsCellDateFormatted(cell))
                                {
                                    try
                                    {
                                        return cell.DateCellValue;
                                    }
                                    catch
                                    {
                                        return cell.NumericCellValue;
                                    }
                                }
                                return cell.NumericCellValue;
                            
                            case CellType.String:
                                return string.IsNullOrEmpty(cell.StringCellValue) ? DBNull.Value : cell.StringCellValue;
                            
                            case CellType.Boolean:
                                return cell.BooleanCellValue;
                            
                            case CellType.Error:
                                return DBNull.Value; // 对于错误类型的公式结果，返回空值
                                
                            default:
                                // 尝试获取公式计算结果
                                string strValue = cell.ToString() ?? string.Empty;
                                return string.IsNullOrEmpty(strValue) ? DBNull.Value : strValue;
                        }
                    }
                    catch
                    {
                        // 如果公式计算失败，尝试获取公式字符串
                        try
                        {
                            return cell.CellFormula ?? string.Empty;
                        }
                        catch
                        {
                            return DBNull.Value;
                        }
                    }
                
                case CellType.Blank:
                    return DBNull.Value;
                
                case CellType.Error:
                    return DBNull.Value; // 对于错误类型的单元格，返回空值
                    
                default:
                    // 尝试获取字符串值
                    string defaultValue = cell.ToString() ?? string.Empty;
                    return string.IsNullOrEmpty(defaultValue) ? DBNull.Value : defaultValue;
            }
        }
        catch (Exception ex)
        {
            // 记录错误并返回空值
            System.Diagnostics.Debug.WriteLine($"获取单元格值失败: {ex.Message}");
            return DBNull.Value;
        }
    }

    /// <summary>
    /// 将DataTable转换为Excel文件并保存
    /// </summary>
    /// <param name="dataTable">要转换的DataTable</param>
    /// <param name="filePath">保存的文件路径</param>
    /// <param name="sheetName">工作表名称，默认为"Sheet1"</param>
    /// <param name="isXlsx">是否保存为xlsx格式，默认为true</param>
    /// <exception cref="ArgumentNullException">数据表或文件路径为空时抛出</exception>
    /// <exception cref="InvalidOperationException">处理转换或保存时出错时抛出</exception>
    public void ConvertToExcel(
        DataTable dataTable,
        string filePath,
        string sheetName = "Sheet1",
        bool isXlsx = true)
    {
        if (dataTable == null)
        {
            throw new ArgumentNullException(nameof(dataTable), "数据表不能为空");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
        }

        if (string.IsNullOrWhiteSpace(sheetName))
        {
            sheetName = "Sheet1";
        }

        try
        {
            // 确保目录存在
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 根据扩展名确定文件类型
            bool useXlsx = isXlsx;
            string extension = Path.GetExtension(filePath).ToLower();
            if (extension == ".xls")
            {
                useXlsx = false;
            }
            else if (extension == ".xlsx")
            {
                useXlsx = true;
            }
            else if (string.IsNullOrEmpty(extension))
            {
                // 如果没有扩展名，根据isXlsx参数添加扩展名
                filePath = filePath + (useXlsx ? ".xlsx" : ".xls");
            }
            
            // 创建工作簿和工作表
            IWorkbook workbook = useXlsx ? new XSSFWorkbook() : (IWorkbook)new HSSFWorkbook();
            ISheet sheet = workbook.CreateSheet(sheetName);
            
            // 转换DataTable数据到工作表
            ConvertDataTableToWorkbook(dataTable, workbook, sheet);
            
            // 保存工作簿
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fileStream);
            }
            
            // 关闭工作簿以释放资源
            workbook.Close();
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"保存Excel文件时发生IO错误: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"将DataTable转换为Excel文件时出错: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 将DataTable转换为Excel并返回内存流
    /// </summary>
    /// <param name="dataTable">要转换的DataTable</param>
    /// <param name="sheetName">工作表名称，默认为"Sheet1"</param>
    /// <param name="isXlsx">是否使用xlsx格式，默认为true</param>
    /// <returns>包含Excel数据的内存流</returns>
    /// <exception cref="ArgumentNullException">数据表为空时抛出</exception>
    /// <exception cref="InvalidOperationException">处理转换时出错时抛出</exception>
    public MemoryStream ConvertToExcelStream(
        DataTable dataTable,
        string sheetName = "Sheet1",
        bool isXlsx = true)
    {
        if (dataTable == null)
        {
            throw new ArgumentNullException(nameof(dataTable), "数据表不能为空");
        }

        if (string.IsNullOrWhiteSpace(sheetName))
        {
            sheetName = "Sheet1";
        }

        try
        {
            // 创建工作簿和工作表
            IWorkbook workbook = isXlsx ? new XSSFWorkbook() : (IWorkbook)new HSSFWorkbook();
            ISheet sheet = workbook.CreateSheet(sheetName);
            
            // 转换DataTable数据到工作表
            ConvertDataTableToWorkbook(dataTable, workbook, sheet);
            
            // 创建新的内存流，不使用using以确保流不会被关闭
            MemoryStream stream = new MemoryStream();
            workbook.Write(stream, true);
            // 重置流位置到开始
            stream.Position = 0;
            
            // 关闭工作簿以释放资源 (NPOI 4.x版本支持)
            workbook.Close();
            
            return stream;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"将DataTable转换为Excel流时出错: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 将多个DataTable转换为Excel的多个工作表
    /// </summary>
    /// <param name="dataTables">要转换的DataTable集合</param>
    /// <param name="filePath">保存的文件路径</param>
    /// <param name="sheetNames">工作表名称集合，如果为null则使用"Sheet1"、"Sheet2"等</param>
    /// <param name="isXlsx">是否保存为xlsx格式，默认为true</param>
    /// <exception cref="ArgumentNullException">数据表集合或文件路径为空时抛出</exception>
    /// <exception cref="ArgumentException">数据表集合为空时抛出</exception>
    /// <exception cref="InvalidOperationException">处理转换或保存时出错时抛出</exception>
    public void ConvertToExcel(
        DataTable[] dataTables,
        string filePath,
        string[]? sheetNames = null,
        bool isXlsx = true)
    {
        if (dataTables == null)
        {
            throw new ArgumentNullException(nameof(dataTables), "数据表集合不能为空");
        }
        
        if (dataTables.Length == 0)
        {
            throw new ArgumentException("数据表集合不能为空", nameof(dataTables));
        }
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
        }
        
        // 检查数据表集合中是否有空项
        for (int i = 0; i < dataTables.Length; i++)
        {
            if (dataTables[i] == null)
            {
                throw new ArgumentException($"数据表集合中索引 {i} 处的数据表为空", nameof(dataTables));
            }
        }

        try
        {
            // 确保目录存在
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 根据扩展名确定文件类型
            bool useXlsx = isXlsx;
            string extension = Path.GetExtension(filePath).ToLower();
            if (extension == ".xls")
            {
                useXlsx = false;
            }
            else if (extension == ".xlsx")
            {
                useXlsx = true;
            }
            else if (string.IsNullOrEmpty(extension))
            {
                // 如果没有扩展名，根据isXlsx参数添加扩展名
                filePath = filePath + (useXlsx ? ".xlsx" : ".xls");
            }
            
            // 创建工作簿
            IWorkbook workbook = useXlsx ? new XSSFWorkbook() : (IWorkbook)new HSSFWorkbook();
            
            // 处理每个DataTable
            for (int i = 0; i < dataTables.Length; i++)
            {
                // 确保工作表名称有效
                string sheetName = "Sheet1";
                if (sheetNames != null && i < sheetNames.Length && !string.IsNullOrWhiteSpace(sheetNames[i]))
                {
                    sheetName = sheetNames[i];
                }
                else
                {
                    sheetName = $"Sheet{i + 1}";
                }
                
                // Excel工作表名称的限制：长度不超过31个字符，不包含某些特殊字符
                sheetName = SanitizeSheetName(sheetName);
                
                // 检查是否有重名的工作表
                int suffix = 1;
                string originalName = sheetName;
                while (workbook.GetSheet(sheetName) != null)
                {
                    // 如果工作表名称已存在，添加后缀
                    sheetName = $"{originalName}_{suffix++}";
                    if (sheetName.Length > 31)
                    {
                        // 确保名称长度不超过31
                        sheetName = $"{originalName.Substring(0, Math.Min(originalName.Length, 27))}_{suffix}";
                    }
                }
                    
                ISheet sheet = workbook.CreateSheet(sheetName);
                ConvertDataTableToWorkbook(dataTables[i], workbook, sheet);
            }
            
            // 保存工作簿
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fileStream);
            }
            
            // 关闭工作簿以释放资源
            workbook.Close();
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"保存Excel文件时发生IO错误: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"将DataTable集合转换为Excel文件时出错: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 清理工作表名称，确保符合Excel工作表命名规则
    /// </summary>
    /// <param name="sheetName">原始工作表名称</param>
    /// <returns>处理后符合Excel命名规则的工作表名称</returns>
    /// <remarks>
    /// Excel工作表命名限制：
    /// 1. 长度不能超过31个字符
    /// 2. 不能包含以下特殊字符: [ ] / \ ? * :
    /// 
    /// 该方法会将不符合规则的字符替换为下划线，并截断超长的名称。
    /// 如果输入为空字符串，则返回默认名称"Sheet1"。
    /// </remarks>
    private string SanitizeSheetName(string sheetName)
    {
        // Excel工作表名称限制：
        // 1. 长度不超过31个字符
        // 2. 不能包含以下字符: []/\?*:
        
        if (string.IsNullOrEmpty(sheetName))
        {
            return "Sheet1";
        }
        
        // 移除不允许的字符
        char[] invalidChars = { '/', '\\', '?', '*', ':', '[', ']' };
        foreach (char c in invalidChars)
        {
            sheetName = sheetName.Replace(c, '_');
        }
        
        // 截断长度
        if (sheetName.Length > 31)
        {
            sheetName = sheetName.Substring(0, 31);
        }
        
        return sheetName;
    }
    
    /// <summary>
    /// 将DataTable数据写入工作簿中的指定工作表
    /// </summary>
    /// <param name="dataTable">要转换的数据表</param>
    /// <param name="workbook">目标工作簿</param>
    /// <param name="sheet">目标工作表</param>
    /// <exception cref="ArgumentNullException">任一参数为空时抛出</exception>
    /// <exception cref="InvalidOperationException">转换过程中出错时抛出</exception>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 创建表头行，并应用粗体样式
    /// 2. 如果存在字段转换器，应用列名的自定义转换规则
    /// 3. 逐行写入数据内容
    /// 4. 自动调整列宽以适应内容，并设置最大列宽限制
    /// 
    /// 该方法会捕获并处理列宽调整过程中可能出现的异常，确保整体转换过程不受影响。
    /// </remarks>
    private void ConvertDataTableToWorkbook(DataTable dataTable, IWorkbook workbook, ISheet sheet)
    {
        if (dataTable == null)
        {
            throw new ArgumentNullException(nameof(dataTable), "数据表不能为空");
        }
        
        if (workbook == null)
        {
            throw new ArgumentNullException(nameof(workbook), "工作簿不能为空");
        }
        
        if (sheet == null)
        {
            throw new ArgumentNullException(nameof(sheet), "工作表不能为空");
        }
        
        try
        {
            // 创建单元格样式
            ICellStyle headerStyle = workbook.CreateCellStyle();
            IFont headerFont = workbook.CreateFont();
            headerFont.IsBold = true;
            headerStyle.SetFont(headerFont);
            
            // 创建表头行
            IRow headerRow = sheet.CreateRow(0);
            
            // 填充表头
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                ICell cell = headerRow.CreateCell(i);
                string columnName = dataTable.Columns[i].ColumnName;
                
                // 应用自定义字段转换器 - 从DataTable到Excel方向
                if (FieldTransformer != null)
                {
                    try
                    {
                        string? excelColumnName = FieldTransformer.TransformDataTableToExcel(
                            new FieldInfo 
                            { 
                                Name = columnName, 
                                Type = dataTable.Columns[i].DataType 
                            });
                            
                        if (!string.IsNullOrWhiteSpace(excelColumnName))
                        {
                            columnName = excelColumnName;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果转换失败，记录错误并使用原始列名
                        System.Diagnostics.Debug.WriteLine($"字段转换失败: {ex.Message}");
                    }
                }
                
                cell.SetCellValue(columnName);
                cell.CellStyle = headerStyle;
            }
            
            // 填充数据行
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                IRow row = sheet.CreateRow(i + 1);
                
                for (int j = 0; j < dataTable.Columns.Count; j++)
                {
                    ICell cell = row.CreateCell(j);
                    object value = dataTable.Rows[i][j];
                    
                    SetCellValue(cell, value);
                }
            }
            
            // 自动调整列宽
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                try
                {
                    sheet.AutoSizeColumn(i);
                    
                    // 设置一个最大列宽，防止单元格内容过大导致的列宽过大问题
                    if (sheet.GetColumnWidth(i) > 15000) // NPOI单位，约250个像素
                    {
                        sheet.SetColumnWidth(i, 15000);
                    }
                }
                catch (Exception ex)
                {
                    // 如果调整列宽失败，记录错误但继续
                    System.Diagnostics.Debug.WriteLine($"调整列宽失败: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"转换DataTable到Excel工作表时出错: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 根据值的类型设置Excel单元格的值
    /// </summary>
    /// <param name="cell">要设置的Excel单元格</param>
    /// <param name="value">待设置的值</param>
    /// <remarks>
    /// 该方法根据值的类型选择合适的单元格赋值方法:
    /// - 字符串类型：直接设置字符串值
    /// - 日期时间类型：设置日期值并应用"yyyy-MM-dd"格式
    /// - 布尔类型：设置布尔值
    /// - 数值类型（double、decimal、float、int、long）：设置对应的数值
    /// - 其他类型：尝试转换为字符串后设置
    /// 
    /// 如果值为null或DBNull，则设置为空字符串。
    /// </remarks>
    private void SetCellValue(ICell cell, object value)
    {
        if (value == null || value == DBNull.Value)
        {
            cell.SetCellValue(string.Empty);
            return;
        }
        
        switch (value)
        {
            case string strValue:
                cell.SetCellValue(strValue);
                break;
                
            case DateTime dateValue:
                cell.SetCellValue(dateValue);
                // 设置日期格式
                ICellStyle dateStyle = cell.Sheet.Workbook.CreateCellStyle();
                IDataFormat dataFormat = cell.Sheet.Workbook.CreateDataFormat();
                dateStyle.DataFormat = dataFormat.GetFormat("yyyy-MM-dd");
                cell.CellStyle = dateStyle;
                break;
                
            case bool boolValue:
                cell.SetCellValue(boolValue);
                break;
                
            case double doubleValue:
                cell.SetCellValue(doubleValue);
                break;
                
            case decimal decimalValue:
                cell.SetCellValue((double)decimalValue);
                break;
                
            case float floatValue:
                cell.SetCellValue(floatValue);
                break;
                
            case int intValue:
                cell.SetCellValue(intValue);
                break;
                
            case long longValue:
                cell.SetCellValue(longValue);
                break;
                
            default:
                // 尝试转换为字符串
                cell.SetCellValue(value.ToString() ?? string.Empty);
                break;
        }
    }
}

/// <summary>
/// 字段信息类，描述DataTable列的名称和数据类型
/// </summary>
public class FieldInfo
{
    /// <summary>
    /// 字段名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 字段数据类型
    /// </summary>
    public Type? Type { get; set; }
}

/// <summary>
/// 字段转换器接口，用于Excel与DataTable之间的字段转换
/// </summary>
public interface IFieldTransformer
{
    /// <summary>
    /// 将Excel列名转换为DataTable的字段信息
    /// </summary>
    /// <param name="excelColumnName">Excel列名</param>
    /// <returns>DataTable字段信息</returns>
    FieldInfo TransformExcelToDataTable(string excelColumnName);
    
    /// <summary>
    /// 将DataTable字段信息转换为Excel列名
    /// </summary>
    /// <param name="fieldInfo">DataTable字段信息</param>
    /// <returns>Excel列名</returns>
    string TransformDataTableToExcel(FieldInfo fieldInfo);
}

/// <summary>
/// 基于字典的字段转换器实现
/// </summary>
public class DictionaryFieldTransformer : IFieldTransformer
{
    private readonly Dictionary<string, FieldInfo> _excelToDataTableMap;
    private readonly Dictionary<string, string> _dataTableToExcelMap;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    public DictionaryFieldTransformer()
    {
        _excelToDataTableMap = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
        _dataTableToExcelMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// 注册字段映射关系
    /// </summary>
    /// <param name="excelColumnName">Excel列名</param>
    /// <param name="dataTableFieldName">DataTable字段名</param>
    /// <param name="dataType">数据类型，默认为string</param>
    public void RegisterMapping(string excelColumnName, string dataTableFieldName, Type? dataType = null)
    {
        if (string.IsNullOrWhiteSpace(excelColumnName))
        {
            throw new ArgumentException("Excel列名不能为空", nameof(excelColumnName));
        }
        
        if (string.IsNullOrWhiteSpace(dataTableFieldName))
        {
            throw new ArgumentException("DataTable字段名不能为空", nameof(dataTableFieldName));
        }
        
        // 注册Excel到DataTable的映射
        _excelToDataTableMap[excelColumnName] = new FieldInfo
        {
            Name = dataTableFieldName,
            Type = dataType ?? typeof(string)
        };
        
        // 注册DataTable到Excel的映射
        _dataTableToExcelMap[dataTableFieldName] = excelColumnName;
    }
    
    /// <summary>
    /// 将Excel列名转换为DataTable的字段信息
    /// </summary>
    /// <param name="excelColumnName">Excel列名</param>
    /// <returns>DataTable字段信息</returns>
    public FieldInfo TransformExcelToDataTable(string excelColumnName)
    {
        // 如果在映射字典中找到匹配项，返回对应的字段信息
        if (_excelToDataTableMap.TryGetValue(excelColumnName, out var fieldInfo))
        {
            return fieldInfo;
        }
        
        // 未找到时返回原始列名和字符串类型
        return new FieldInfo
        {
            Name = excelColumnName,
            Type = typeof(string)
        };
    }
    
    /// <summary>
    /// 将DataTable字段信息转换为Excel列名
    /// </summary>
    /// <param name="fieldInfo">DataTable字段信息</param>
    /// <returns>Excel列名</returns>
    public string TransformDataTableToExcel(FieldInfo fieldInfo)
    {
        // 如果在映射字典中找到匹配项，返回对应的Excel列名
        if (_dataTableToExcelMap.TryGetValue(fieldInfo.Name, out var excelColumnName))
        {
            return excelColumnName;
        }
        
        // 未找到时返回原始字段名
        return fieldInfo.Name;
    }
}

/// <summary>
/// 自定义字段转换器，通过自定义函数实现转换逻辑
/// </summary>
public class CustomFieldTransformer : IFieldTransformer
{
    private readonly Func<string, FieldInfo> _excelToDataTableFunc;
    private readonly Func<FieldInfo, string> _dataTableToExcelFunc;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="excelToDataTableFunc">Excel到DataTable的转换函数</param>
    /// <param name="dataTableToExcelFunc">DataTable到Excel的转换函数</param>
    public CustomFieldTransformer(
        Func<string, FieldInfo> excelToDataTableFunc,
        Func<FieldInfo, string> dataTableToExcelFunc)
    {
        _excelToDataTableFunc = excelToDataTableFunc ?? throw new ArgumentNullException(nameof(excelToDataTableFunc));
        _dataTableToExcelFunc = dataTableToExcelFunc ?? throw new ArgumentNullException(nameof(dataTableToExcelFunc));
    }
    
    /// <summary>
    /// 将Excel列名转换为DataTable的字段信息
    /// </summary>
    /// <param name="excelColumnName">Excel列名</param>
    /// <returns>DataTable字段信息</returns>
    public FieldInfo TransformExcelToDataTable(string excelColumnName)
    {
        return _excelToDataTableFunc(excelColumnName);
    }
    
    /// <summary>
    /// 将DataTable字段信息转换为Excel列名
    /// </summary>
    /// <param name="fieldInfo">DataTable字段信息</param>
    /// <returns>Excel列名</returns>
    public string TransformDataTableToExcel(FieldInfo fieldInfo)
    {
        return _dataTableToExcelFunc(fieldInfo);
    }
}
