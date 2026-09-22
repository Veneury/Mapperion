using System;
using System.Linq.Expressions;

namespace Mapperion
{
    /// <summary>
    /// Configures how one constructor parameter of the destination is supplied.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    public interface ICtorParamConfigurationExpression<TSource>
    {
        /// <summary>Takes the argument from the given source expression.</summary>
        /// <typeparam name="TMember">The type the expression produces.</typeparam>
        /// <param name="sourceMember">The expression that reads the value.</param>
        void MapFrom<TMember>(Expression<Func<TSource, TMember>> sourceMember);

        /// <summary>Supplies a fixed argument.</summary>
        /// <param name="value">The value to pass.</param>
        void UseValue(object value);
    }
}
