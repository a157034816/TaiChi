using System;
using System.Linq.Expressions;

namespace TaiChi.Cache.Internal
{
    /// <summary>
    /// 表达式树辅助方法（用于组合谓词并统一参数实例）。
    /// </summary>
    internal static class ExpressionHelper
    {
        /// <summary>
        /// 使用 AndAlso 组合两个谓词表达式，并将两侧参数替换为同一个参数实例。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="left">左侧谓词。</param>
        /// <param name="right">右侧谓词。</param>
        /// <returns>组合后的谓词。</returns>
        public static Expression<Func<T, bool>> AndAlso<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));

            var param = Expression.Parameter(typeof(T), "x");
            var leftBody = new ParameterReplaceVisitor(left.Parameters[0], param).Visit(left.Body);
            var rightBody = new ParameterReplaceVisitor(right.Parameters[0], param).Visit(right.Body);

            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody!, rightBody!), param);
        }

        /// <summary>
        /// 尝试组合两个谓词表达式（任一为 null 时返回另一侧）。
        /// </summary>
        public static Expression<Func<T, bool>>? TryAndAlso<T>(Expression<Func<T, bool>>? left, Expression<Func<T, bool>>? right)
        {
            if (left == null) return right;
            if (right == null) return left;
            return AndAlso(left, right);
        }

        /// <summary>
        /// 参数替换访问器：将表达式中的某个参数替换为目标参数。
        /// </summary>
        private sealed class ParameterReplaceVisitor : ExpressionVisitor
        {
            /// <summary>
            /// 需要被替换的源参数。
            /// </summary>
            private readonly ParameterExpression _from;

            /// <summary>
            /// 替换后的目标参数。
            /// </summary>
            private readonly ParameterExpression _to;

            /// <summary>
            /// 创建参数替换访问器。
            /// </summary>
            public ParameterReplaceVisitor(ParameterExpression from, ParameterExpression to)
            {
                _from = from;
                _to = to;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _from)
                {
                    return _to;
                }

                return base.VisitParameter(node);
            }
        }
    }
}

