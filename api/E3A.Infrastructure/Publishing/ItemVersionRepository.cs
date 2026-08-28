using Core.EntityFrameworkCore.Repositories;
using E3A.Domain.Publishing;
using E3A.Infrastructure.Data.Context;

namespace E3A.Infrastructure.Publishing;

public class ItemVersionRepository(AppDbContext context) : Repository<ItemVersion>(context), IItemVersionRepository { }
