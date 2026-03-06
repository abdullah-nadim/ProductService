namespace Core.Contracts
{
    public abstract class BaseContractFilter<TContract, TModel> : BaseContract<TContract, TModel>
        where TContract : class where TModel : class
    {
        protected BaseContractFilter()
        {
            AllowPaging = false;
            PageNumber = PageSize = 0;
        }

        public bool AllowPaging { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
