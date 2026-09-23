using System.Collections.Generic;

namespace Mapperion.Benchmarks
{
    /// <summary>
    /// The two compile-time mappers in the comparison. Both attributes are spelled out in full:
    /// Mapperion and Mapperly each call theirs <c>Mapper</c>, and having both generators attached
    /// to one project is the whole point here.
    /// </summary>
    [Mapperion.Mapper]
    public partial class MapperionGenerated
    {
        public partial FlatDto ToDto(Flat source);

        public partial LineDto ToDto(Line source);

        public partial List<LineDto> ToDtos(IList<Line> source);

        public partial LineRecordDto ToRecord(Line source);
    }

    [Riok.Mapperly.Abstractions.Mapper]
    public partial class MapperlyGenerated
    {
        public partial FlatDto ToDto(Flat source);

        public partial LineDto ToDto(Line source);

        public partial List<LineDto> ToDtos(List<Line> source);

        public partial LineRecordDto ToRecord(Line source);
    }
}
