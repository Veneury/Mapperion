using System;
using System.Linq.Expressions;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Configuration
{
    /// <summary>
    /// Mutable state gathered for one destination member while the configuration callback runs.
    /// </summary>
    internal interface IMemberConfiguration
    {
        MemberDescriptor DestinationMember { get; }

        MemberPath? DestinationPath { get; }

        MemberDefinition Build();
    }

    /// <inheritdoc cref="IMemberConfiguration" />
    internal sealed class MemberConfiguration<TSource, TDestination, TMember>
        : IMemberConfigurationExpression<TSource, TDestination, TMember>, IMemberConfiguration
    {
        private MemberSource? source;
        private bool isIgnored;
        private bool useDestinationValue;
        private bool hasNullSubstitute;
        private object? nullSubstitute;
        private object? condition;
        private object? preCondition;
        private Type? valueConverterType;
        private int mappingOrder;

        internal MemberConfiguration(MemberDescriptor destinationMember)
            : this(destinationMember, null)
        {
        }

        internal MemberConfiguration(MemberDescriptor destinationMember, MemberPath? destinationPath)
        {
            DestinationMember = destinationMember;
            DestinationPath = destinationPath;
        }

        public MemberDescriptor DestinationMember { get; }

        public MemberPath? DestinationPath { get; }

        public void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        {
            Guard.NotNull(sourceMember, nameof(sourceMember));

            source = MemberExpressionParser.ParseSource(sourceMember);
            isIgnored = false;
        }

        public void MapFrom<TValueResolver>()
            where TValueResolver : IValueResolver<TSource, TDestination, TMember>
        {
            source = new ValueResolverSource(typeof(TValueResolver), typeof(TMember));
            isIgnored = false;
        }

        public void ConvertUsing<TValueConverter, TSourceMember>()
            where TValueConverter : IValueConverter<TSourceMember, TMember>
        {
            valueConverterType = typeof(TValueConverter);
        }

        public void Ignore()
        {
            isIgnored = true;
            source = null;
        }

        public void Condition(Expression<Func<TSource, bool>> condition)
        {
            this.condition = Guard.NotNull(condition, nameof(condition));
        }

        public void PreCondition(Expression<Func<TSource, bool>> condition)
        {
            preCondition = Guard.NotNull(condition, nameof(condition));
        }

        public void NullSubstitute(object value)
        {
            nullSubstitute = Guard.NotNull(value, nameof(value));
            hasNullSubstitute = true;
        }

        public void SetMappingOrder(int order)
        {
            mappingOrder = order;
        }

        public void UseDestinationValue()
        {
            useDestinationValue = true;
        }

        public MemberDefinition Build()
        {
            return new MemberDefinition(DestinationMember)
            {
                DestinationPath = DestinationPath,
                Source = source,
                IsIgnored = isIgnored,
                IsExplicit = true,
                UseDestinationValue = useDestinationValue,
                MappingOrder = mappingOrder,
                HasNullSubstitute = hasNullSubstitute,
                NullSubstitute = nullSubstitute,
                Condition = condition,
                PreCondition = preCondition,
                ValueConverterType = valueConverterType,
            };
        }
    }
}
