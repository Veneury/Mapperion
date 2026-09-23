using System.Linq.Expressions;

namespace Mapperion.Internal
{
    /// <summary>
    /// Rewrites a user-supplied lambda so its parameter becomes the expression the compiler is
    /// already working with. Inlining the body this way avoids an <c>Invoke</c> node, which costs a
    /// delegate call at run time and is not translatable by LINQ providers.
    /// </summary>
    internal sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression parameter;
        private readonly Expression replacement;

        private ParameterReplacer(ParameterExpression parameter, Expression replacement)
        {
            this.parameter = parameter;
            this.replacement = replacement;
        }

        internal static Expression Inline(LambdaExpression lambda, Expression argument)
        {
            ParameterExpression parameter = lambda.Parameters[0];

            Expression replacement = parameter.Type != argument.Type && parameter.Type.IsAssignableFrom(argument.Type)
                ? Expression.Convert(argument, parameter.Type)
                : argument;

            var replacer = new ParameterReplacer(parameter, replacement);
            return replacer.Visit(lambda.Body);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == parameter ? replacement : base.VisitParameter(node);
        }
    }
}
