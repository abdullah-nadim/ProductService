namespace ProductService.Models.Repositories;

public interface IRepositoryFactory : IDisposable
{
    IProductRepository GetProductRepository();
    int Commit();
}
