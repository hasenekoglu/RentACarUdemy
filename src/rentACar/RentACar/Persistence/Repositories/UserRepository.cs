﻿using Application.Services.Repositories;
using Core.Persistence.Repositories;
using Core.Security.Entities;
using Persistence.Context;

namespace Persistence.Repositories;

public class UserRepository : EfRepositoryBase<User,int,BaseDbContext>, IUserRepository
{
    public UserRepository(BaseDbContext context) : base(context)
    {
    }
}