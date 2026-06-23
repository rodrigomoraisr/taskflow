namespace TaskFlow.Application.Tasks;

public class GetTasksResponse
{
    public List<GetTaskResponse> Items{get;set;} = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}