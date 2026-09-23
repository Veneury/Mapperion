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

        /// <summary>
        /// Takes the value from a resolver, which sees the whole source and destination.
        /// </summary>
        /// <typeparam name="TValueResolver">The resolver, which needs a parameterless constructor.</typeparam>
        void MapFrom<TValueResolver>()
            where TValueResolver : IValueResolver<TSource, TDestination, TMember>;

        /// <summary>
        /// Runs the resolved value through a converter before assigning it.
        /// </summary>
        /// <typeparam name="TValueConverter">The converter, which needs a parameterless constructor.</typeparam>
        /// <typeparam name="TSourceMember">The type the converter reads.</typeparam>
        void ConvertUsing<TValueConverter, TSourceMember>()
            where TValueConverter : IValueConverter<TSourceMember, TMember>;

        /// <summary>Leaves the member unmapped.</summary>
        void Ignore();

        /// <summary>
        /// Assigns the member only when the predicate holds. The source value is read first, so a
        /// condition costs the read even when it turns out false; use
        /// <see cref="PreCondition"/> when the read itself is what you want to avoid.
        /// </summary>
        /// <param name="condition">The predicate evaluated against the source.</param>
        void Condition(Expression<Func<TSource, bool>> condition);

        /// <summary>
        /// Skips the member entirely, before its source is even read, when the predicate does not
        /// hold. Useful when reading the source is expensive or would throw.
        /// </summary>
        /// <param name="condition">The predicate evaluated against the source.</param>
        void PreCondition(Expression<Func<TSource, bool>> condition);

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
