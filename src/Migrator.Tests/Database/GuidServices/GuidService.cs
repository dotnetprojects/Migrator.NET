using System;
using Migrator.Tests.Database.GuidServices.Interfaces;

namespace Migrator.Tests.Database.GuidServices;

public class GuidService : IGuidService
{
    public Guid NewGuid()
    {
        return Guid.NewGuid();
    }
}