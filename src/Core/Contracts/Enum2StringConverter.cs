using AutoMapper;

namespace Core.Contracts
{
    public class Enum2StringConverter<TEnum> : ITypeConverter<TEnum, string>
    {
        public string Convert(TEnum source, string destination, ResolutionContext context)
        {
            return source.ToString();
        }
    }
}
