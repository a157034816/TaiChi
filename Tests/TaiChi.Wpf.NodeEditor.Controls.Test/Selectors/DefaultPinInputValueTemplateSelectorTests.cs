using System;
using System.Windows;
using System.Windows.Controls;
using TaiChi.Wpf.NodeEditor.Controls.Selectors;
using TaiChi.Wpf.NodeEditor.Core.Models;
using Xunit;

namespace TaiChi.Wpf.NodeEditor.Controls.Test.Selectors
{
    /// <summary>
    /// DefaultPinInputValueTemplateSelector 单元测试
    /// </summary>
    public class DefaultPinInputValueTemplateSelectorTests
    {
        private readonly DefaultPinInputValueTemplateSelector _selector;
        private readonly DataTemplate _mockTemplate;

        public DefaultPinInputValueTemplateSelectorTests()
        {
            _selector = new DefaultPinInputValueTemplateSelector();
            _mockTemplate = new DataTemplate();
        }

        [Fact]
        public void SelectTemplate_WithNullItem_ReturnsNull()
        {
            // Arrange
            FrameworkElement? container = null;

            // Act
            var result = _selector.SelectTemplate(null, container);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SelectTemplate_WithNullContainer_ReturnsNull()
        {
            // Arrange
            var pin = new Pin { DataType = typeof(string) };

            // Act
            var result = _selector.SelectTemplate(pin, null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SelectTemplate_WithNonPinItem_ReturnsDefaultTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var nonPinItem = new object();
            _selector.DefaultTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(nonPinItem, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfStringType_ReturnsStringTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(string) };
            _selector.StringTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfIntType_ReturnsIntTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(int) };
            _selector.IntTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfLongType_ReturnsLongTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(long) };
            _selector.LongTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfFloatType_ReturnsFloatTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(float) };
            _selector.FloatTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfDoubleType_ReturnsDoubleTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(double) };
            _selector.DoubleTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfDecimalType_ReturnsDecimalTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(decimal) };
            _selector.DecimalTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfBoolType_ReturnsBoolTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(bool) };
            _selector.BoolTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfDateTimeType_ReturnsDateTimeTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(DateTime) };
            _selector.DateTimeTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfEnumType_ReturnsEnumTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(TestEnum) };
            _selector.EnumTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithPinOfUnsupportedType_ReturnsDefaultTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(Guid) }; // 不支持的类型
            _selector.DefaultTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithNullDataType_ReturnsDefaultTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = null! }; // 强制设置为null
            _selector.DefaultTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithMissingTemplate_ReturnsDefaultTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(int) };
            // 不设置 IntTemplate，只设置 DefaultTemplate
            _selector.DefaultTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithNullableIntType_ReturnsIntTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(int?) };
            _selector.IntTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        [Fact]
        public void SelectTemplate_WithNullableBoolType_ReturnsBoolTemplate()
        {
            // Arrange
            FrameworkElement? container = null;
            var pin = new Pin { DataType = typeof(bool?) };
            _selector.BoolTemplate = _mockTemplate;

            // Act
            var result = _selector.SelectTemplate(pin, container);

            // Assert
            Assert.Equal(_mockTemplate, result);
        }

        /// <summary>
        /// 测试用的枚举类型
        /// </summary>
        private enum TestEnum
        {
            Value1,
            Value2,
            Value3
        }
    }
}