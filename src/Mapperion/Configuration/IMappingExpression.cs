using System;
using System.Linq.Expressions;
using Mapperion.Model;

namespace Mapperion
{
    /// <summary>
    /// Configures the map between one source type and one destination type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    public interface IMappingExpression<TSource, TDestination>
    {
        /// <summary>Configures a single destination member.</summary>
        /// <typeparam name="TMember">The destination member type.</typeparam>
        /// <param name="destinationMember">A direct member access on the destination, such as <c>d =&gt; d.Total</c>.</param>
        /// <param name="memberOptions">The configuration applied to that member.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        /// <exception cref="MapperConfigurationException"><paramref name="destinationMember"/> is not a direct member access.</exception>
        IMappingExpression<TSource, TDestination> ForMember<TMember>(
            Expression<Func<TDestination, TMember>> destinationMember,
            Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions);

        /// <summary>
        /// Configures one parameter of the destination constructor. Parameter names are matched
        /// ignoring case, because C# names parameters in camelCase and properties in PascalCase.
        /// </summary>
        /// <param name="constructorParameterName">The parameter name, as declared.</param>
        /// <param name="parameterOptions">The configuration applied to that parameter.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        IMappingExpression<TSource, TDestination> ForCtorParam(
            string constructorParameterName,
            Action<ICtorParamConfigurationExpression<TSource>> parameterOptions);

        /// <summary>
        /// Declares the map in the opposite direction and returns it, so it can be configured
        /// further. Members configured here with a plain <c>MapFrom</c> onto a single writable
        /// source member are inverted; flattened paths, arbitrary expressions and ignored members
        /// are not, and the reverse resolves those by convention instead.
        /// </summary>
        /// <returns>The expression configuring the reverse map.</returns>
        /// <exception cref="MapperConfigurationException">The reverse pair was already declared.</exception>
        IMappingExpression<TDestination, TSource> ReverseMap();

        /// <summary>
        /// Replaces this map entirely with a converter. Member configuration stops applying: the
        /// converter produces the destination on its own.
        /// </summary>
        /// <typeparam name="TTypeConverter">The converter, which needs a parameterless constructor.</typeparam>
        /// <returns>This expression, for chaining.</returns>
        IMappingExpression<TSource, TDestination> ConvertUsing<TTypeConverter>()
            where TTypeConverter : ITypeConverter<TSource, TDestination>;

        /// <summary>Sets which side must be fully covered for this map to validate.</summary>
        /// <param name="validation">The validation mode.</param>
        /// <returns>This expression, for chaining.</returns>
        IMappingExpression<TSource, TDestination> ValidateMemberList(MemberListValidation validation);

        /// <summary>Limits how deep recursive maps descend.</summary>
        /// <param name="depth">The maximum depth.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is not positive.</exception>
        IMappingExpression<TSource, TDestination> MaxDepth(int depth);

        /// <summary>Tracks already-mapped instances so reference cycles terminate.</summary>
        /// <returns>This expression, for chaining.</returns>
        IMappingExpression<TSource, TDestination> PreserveReferences();
    }
}
