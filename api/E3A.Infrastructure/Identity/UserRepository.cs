using Core.EntityFrameworkCore.Repositories;
using E3A.Domain.Identity;
using E3A.Infrastructure.Data.Context;

namespace E3A.Infrastructure.Identity;

public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository { }
