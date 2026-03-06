using AutoMapper;

namespace Core.Contracts
{
    public abstract class BaseContractMapper<TContract, TModel> where TContract : class where TModel : class
    {
        public BaseContractMapper(IMapper mapper) { _Mapper = mapper; }
        
        public TContract ToContract(TModel model)
        {
            return _Mapper.Map<TContract>(model);
        }

        public List<TContract> ToContracts(List<TModel> models)
        {
            return _Mapper.Map<List<TContract>>(models);
        }

        public List<TModel> ToModels(List<TContract> contracts)
        {
            return _Mapper.Map<List<TModel>>(contracts);
        }

        public TModel ToModel(TContract contract)
        {
            return _Mapper.Map<TModel>(contract);
        }

        private readonly IMapper _Mapper;
    }
}
