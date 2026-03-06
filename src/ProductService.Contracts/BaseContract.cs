using AutoMapper;

namespace ProductService.Contracts;

public abstract class BaseContract<TContract, TModel>
    where TContract : class
    where TModel : class
{
    public static TContract ToContract(TModel model, IMapper mapper)
        => mapper.Map<TContract>(model);

    public static List<TContract> ToContracts(List<TModel> models, IMapper mapper)
        => mapper.Map<List<TContract>>(models);

    public static List<TModel> ToModels(List<TContract> contracts, IMapper mapper)
        => mapper.Map<List<TModel>>(contracts);

    public TModel ToModel(IMapper mapper)
        => mapper.Map<TModel>(this);
}
