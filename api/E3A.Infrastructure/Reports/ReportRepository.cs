using Core.EntityFrameworkCore.Repositories;
using E3A.Domain.Reports;
using E3A.Infrastructure.Data.Context;

namespace E3A.Infrastructure.Reports;

public class ReportRepository(AppDbContext context) : Repository<Report>(context), IReportRepository { }
