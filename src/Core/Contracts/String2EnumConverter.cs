using AutoMapper;

namespace Core.Contracts
{
    public class String2EnumConverter<TEnum> : ITypeConverter<string, TEnum>
    {
        public TEnum Convert(string source, TEnum destination, ResolutionContext context)
        {
            return (TEnum)Enum.Parse(typeof(TEnum), source);
        }
    }
}
