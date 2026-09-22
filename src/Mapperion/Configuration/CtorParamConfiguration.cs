using System;
using System.Linq.Expressions;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Configuration
{
    /// <summary>
    /// Mutable state gathered for one constructor parameter while the configuration callback runs.
    /// The position is only known once a constructor has been chosen, so it is filled in later.
    /// </summary>
    internal interface ICtorParamConfiguration
    {
        string Name { get; }

        MemberSource? Source { get; }
    }

    /// <inheritdoc cref="ICtorParamConfiguration" />
    internal sealed class CtorParamConfiguration<TSource>
        : ICtorParamConfigurationExpression<TSource>, ICtorParamConfiguration
    {
        internal CtorParamConfiguration(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public MemberSource? Source { get; private set; }

        public void MapFrom<TMember>(Expression<Func<TSource, TMember>> sourceMember)
        {
            Guard.NotNull(sourceMember, nameof(sourceMember));
            Source = MemberExpressionParser.ParseSource(sourceMember);
        }

        public void UseValue(object value)
        {
            Guard.NotNull(value, nameof(value));
            Source = new ConstantSource(value, value.GetType());
        }
    }
}
