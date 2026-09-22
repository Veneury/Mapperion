using System;
using System.Linq.Expressions;

namespace Mapperion
{
    /// <summary>
    /// Configures how one destination member is populated.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <typeparam name="TMember">The destination member type.</typeparam>
    public interface IMemberConfigurationExpression<TSource, TDestination, TMember>
    {
        /// <summary>
        /// Takes the value from the given source expression. A plain member chain such as
        /// <c>s =&gt; s.Customer.Name</c> is recorded as a member path the compiler can inline;
        /// anything else is kept as an opaque expression.
        /// </summary>
        /// <typeparam name="TSourceMember">The type the expression produces.</typeparam>
        /// <param name="sourceMember">The expression that reads the value.</param>
        void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);

        /// <summary>Leaves the member unmapped.</summary>
        void Ignore();

        /// <summary>Assigns the member only when the predicate holds.</summary>
        /// <param name="condition">The predicate evaluated against the source.</param>
        void Condition(Expression<Func<TSource, bool>> condition);

        /// <summary>Substitutes a value when the source resolves to null.</summary>
        /// <param name="value">The replacement value.</param>
        void NullSubstitute(object value);

        /// <summary>Sets the order in which this member is assigned relative to the others.</summary>
        /// <param name="order">The order. Lower runs first.</param>
        void SetMappingOrder(int order);

        /// <summary>Populates the existing destination value instead of creating a new instance.</summary>
        void UseDestinationValue();
    }
}
