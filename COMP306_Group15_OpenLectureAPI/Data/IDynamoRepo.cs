namespace COMP306_Group15_OpenLectureAPI.Data
{
    using Amazon.DynamoDBv2.DataModel;
    using Amazon.DynamoDBv2.DocumentModel;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IDynamoRepo<T> where T : class
    {
        Task<List<T>> GetAllAsync();
        Task<T?> GetByIdAsync(string id);
        Task CreateAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(string id);
    }

    public class DynamoRepo<T> : IDynamoRepo<T> where T : class
    {
        private readonly IDynamoDBContext _ctx;
        public DynamoRepo(IDynamoDBContext ctx) => _ctx = ctx;

        public Task<List<T>> GetAllAsync() =>
            _ctx.ScanAsync<T>(new List<ScanCondition>()).GetRemainingAsync();

        public Task<T?> GetByIdAsync(string id) =>
            _ctx.LoadAsync<T>(id); // returns null if not found

        public Task CreateAsync(T entity) =>
            _ctx.SaveAsync(entity);

        public Task UpdateAsync(T entity) =>
            _ctx.SaveAsync(entity);

        public async Task DeleteAsync(string id)
        {
            var entity = await _ctx.LoadAsync<T>(id);
            if (entity != null) await _ctx.DeleteAsync(entity);
        }
    }
}
