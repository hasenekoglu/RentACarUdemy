using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Persistence.Repositories;

namespace Core.Security.Entities;

public class UserOperationClaim :Entity<int>
{

    public UserOperationClaim(int operationClaimId, int userId)
    {
        OperationClaimId = operationClaimId;
        UserId = userId;
    } 

    public UserOperationClaim(int id,int operationClaimId, int userId):base(id)
    {
        OperationClaimId = operationClaimId;
        UserId = userId;
    }

    public int UserId { get; set; }
    public int OperationClaimId { get; set; }

    public virtual User User { get; set; }
    public virtual OperationClaim OperationClaim { get; set; }

}
