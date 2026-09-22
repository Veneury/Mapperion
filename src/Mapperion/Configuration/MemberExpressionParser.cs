using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Mapperion.Model;

namespace Mapperion.Configuration
{
    /// <summary>
    /// Turns the lambdas written in the fluent API into model elements, so that
    /// <c>System.Linq.Expressions</c> never reaches the model itself.
    /// </summary>
    internal static class MemberExpressionParser
    {
        internal static MemberDescriptor ParseDestinationMember(LambdaExpression selector)
        {
            Expression body = Unwrap(selector.Body);

            if (body is MemberExpression member && member.Expression is ParameterExpression)
            {
                return Describe(member.Member);
            }

            throw new MapperConfigurationException(
                "A destination member must be a direct member access such as 'd => d.Total', but was '" + selector.Body + "'.");
        }

        internal static MemberSource ParseSource(LambdaExpression selector)
        {
            if (TryParsePath(selector.Body, out MemberPath? path))
            {
                return new MemberPathSource(path!);
            }

            return new CustomSource(selector, selector.ReturnType, selector.Body.ToString());
        }

        private static bool TryParsePath(Expression body, out MemberPath? path)
        {
            var steps = new List<MemberDescriptor>();
            Expression current = Unwrap(body);

            while (true)
            {
                if (current is MemberExpression member && member.Expression is not null)
                {
                    steps.Add(Describe(member.Member));
                    current = Unwrap(member.Expression);
                    continue;
                }

                if (current is MethodCallExpression call &&
                    call.Object is not null &&
                    call.Arguments.Count == 0 &&
                    call.Method.ReturnType != typeof(void))
                {
                    steps.Add(MemberDescriptor.ForMethod(call.Method));
                    current = Unwrap(call.Object);
                    continue;
                }

                break;
            }

            if (steps.Count == 0 || current is not ParameterExpression)
            {
                path = null;
                return false;
            }

            steps.Reverse();
            path = new MemberPath(steps);
            return true;
        }

        private static MemberDescriptor Describe(MemberInfo member)
        {
            return member switch
            {
                PropertyInfo property => MemberDescriptor.ForProperty(property),
                FieldInfo field => MemberDescriptor.ForField(field),
                MethodInfo method => MemberDescriptor.ForMethod(method),
                _ => throw new MapperConfigurationException(
                    "Members of kind '" + member.MemberType + "' cannot take part in a map: " + member.Name + "."),
            };
        }

        private static Expression Unwrap(Expression expression)
        {
            while (expression is UnaryExpression unary &&
                   (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                expression = unary.Operand;
            }

            return expression;
        }
    }
}
