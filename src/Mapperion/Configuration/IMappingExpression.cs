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
        /// Configures a member that sits inside the destination rather than on it, such as
        /// <c>d =&gt; d.Customer.Name</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The objects along the path are created as needed, so the destination does not have to
        /// arrive with them already in place. A step that cannot be written and is null has to be
        /// there: nothing can put it there, and the map says so when it runs.
        /// </para>
        /// <para>
        /// Paths are assigned after every direct member, so configuring both the whole object and
        /// something inside it leaves the path with the last word rather than depending on
        /// declaration order. A single-step path is just a member, and is treated as one.
        /// </para>
        /// </remarks>
        /// <typeparam name="TMember">The type of the member at the end of the path.</typeparam>
        /// <param name="destinationPath">A chain of member accesses on the destination.</param>
        /// <param name="pathOptions">The configuration applied to the member at the end.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        /// <exception cref="MapperConfigurationException">
        /// <paramref name="destinationPath"/> is not a chain of member accesses, ends on something
        /// that cannot be written, or goes through a value type or a method call.
        /// </exception>
        IMappingExpression<TSource, TDestination> ForPath<TMember>(
            Expression<Func<TDestination, TMember>> destinationPath,
            Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> pathOptions);

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

        /// <summary>
        /// Builds the destination with the given factory instead of calling a constructor.
        /// </summary>
        /// <remarks>
        /// The factory only runs when the destination has to be created. Mapping onto an instance
        /// the caller already passed in uses that instance, as it does without a factory. Members
        /// are still assigned afterwards, so a factory that fills some of them will see them
        /// overwritten by whatever the map resolves for them; ignore those members if the factory
        /// is meant to have the last word.
        /// </remarks>
        /// <param name="factory">Produces the destination from the source.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
        IMappingExpression<TSource, TDestination> ConstructUsing(Func<TSource, TDestination> factory);

        /// <summary>
        /// Builds the destination with the given factory, which also receives the running
        /// operation so it can map nested values itself.
        /// </summary>
        /// <param name="factory">Produces the destination from the source.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
        IMappingExpression<TSource, TDestination> ConstructUsing(
            Func<TSource, ResolutionContext, TDestination> factory);

        /// <summary>Runs a step before the members are assigned, once the destination exists.</summary>
        /// <param name="action">The step to run.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action);

        /// <summary>Runs a step before the members are assigned, with access to the running mapper.</summary>
        /// <param name="action">The step to run.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination, ResolutionContext> action);

        /// <summary>Runs a step of its own type before the members are assigned.</summary>
        /// <typeparam name="TMappingAction">The step, which needs a parameterless constructor.</typeparam>
        /// <returns>This expression, for chaining.</returns>
        IMappingExpression<TSource, TDestination> BeforeMap<TMappingAction>()
            where TMappingAction : IMappingAction<TSource, TDestination>;

        /// <summary>Runs a step once every member has been assigned.</summary>
        /// <param name="action">The step to run.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action);

        /// <summary>Runs a step once every member has been assigned, with access to the running mapper.</summary>
        /// <param name="action">The step to run.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination, ResolutionContext> action);

        /// <summary>Runs a step of its own type once every member has been assigned.</summary>
        /// <typeparam name="TMappingAction">The step, which needs a parameterless constructor.</typeparam>
        /// <returns>This expression, for chaining.</returns>
        IMappingExpression<TSource, TDestination> AfterMap<TMappingAction>()
            where TMappingAction : IMappingAction<TSource, TDestination>;

        /// <summary>
        /// Declares that a source of the derived type should be mapped through the derived map
        /// instead of this one, so mapping through a base reference still produces the right
        /// destination type.
        /// </summary>
        /// <typeparam name="TDerivedSource">A type deriving from <typeparamref name="TSource"/>.</typeparam>
        /// <typeparam name="TDerivedDestination">A type deriving from <typeparamref name="TDestination"/>.</typeparam>
        /// <returns>This expression, for chaining.</returns>
        IMappingExpression<TSource, TDestination> Include<TDerivedSource, TDerivedDestination>()
            where TDerivedSource : TSource
            where TDerivedDestination : TDestination;

        /// <summary>
        /// Names source members whose own maps to this destination fill in whatever this map leaves
        /// unresolved, which is how one destination is built out of several nested source objects.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This map is looked at first: anything it configures explicitly, and anything its own
        /// conventions resolve, wins. Only members still without a source are offered to the
        /// included members, in the order given here, and the first one that has something to say
        /// provides it. A member the included map ignores counts as having nothing to say.
        /// </para>
        /// <para>
        /// A map declared for the included type is used when there is one, so its renames and
        /// converters carry over. When there is none, the member is matched against the included
        /// type by the same conventions as anywhere else. A null included member leaves the members
        /// it would have filled at their default.
        /// </para>
        /// </remarks>
        /// <param name="members">Source members, each a plain member access such as <c>s =&gt; s.Inner</c>.</param>
        /// <returns>This expression, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="members"/>, or one of its entries, is <see langword="null"/>.</exception>
        /// <exception cref="MapperConfigurationException">An entry is not a chain of member accesses.</exception>
        IMappingExpression<TSource, TDestination> IncludeMembers(
            params Expression<Func<TSource, object?>>[] members);

        /// <summary>
        /// Takes the member configuration of the base map as a starting point. Anything configured
        /// here wins over what the base said.
        /// </summary>
        /// <typeparam name="TBaseSource">The base source type.</typeparam>
        /// <typeparam name="TBaseDestination">The base destination type.</typeparam>
        /// <returns>This expression, for chaining.</returns>
        IMappingExpression<TSource, TDestination> IncludeBase<TBaseSource, TBaseDestination>();

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
