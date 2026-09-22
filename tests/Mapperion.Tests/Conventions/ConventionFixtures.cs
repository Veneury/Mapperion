namespace Mapperion.Tests.Conventions
{
    public sealed class Contact
    {
        public string Email { get; set; } = string.Empty;
    }

    public sealed class Company
    {
        public string Name { get; set; } = string.Empty;

        public Contact Contact { get; set; } = new Contact();
    }

    public sealed class Person
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public Contact Contact { get; set; } = new Contact();

        public Company Company { get; set; } = new Company();

        public string GetBadge() => "badge";

        public string ReadOnly { get; } = string.Empty;

        public string PrivatelySet { get; private set; } = string.Empty;
    }

    public sealed class PersonDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string ContactEmail { get; set; } = string.Empty;

        public string CompanyContactEmail { get; set; } = string.Empty;

        public string Unmatched { get; set; } = string.Empty;

        public string Computed { get; } = string.Empty;
    }

    public sealed class LowercaseDto
    {
        public string name { get; set; } = string.Empty;
    }

    public sealed class BadgeDto
    {
        public string Badge { get; set; } = string.Empty;
    }

    public sealed class PersonDtoSuffixed
    {
        public string NameDto { get; set; } = string.Empty;
    }

    public sealed class PersonProfile : Profile
    {
        public PersonProfile()
        {
            CreateMap<Person, PersonDto>()
                .ForMember(d => d.Unmatched, o => o.Ignore());
        }
    }

    public sealed class BadgeProfile : Profile
    {
        public BadgeProfile()
        {
            CreateMap<Person, BadgeDto>();
        }
    }
}
