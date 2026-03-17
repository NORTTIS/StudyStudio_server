namespace StudioStudio_Server.Models.DTOs.Response
{
    public class StudioListSubscriptionResponse
    {
        public int StudioLimit { get; set; }
        public int StudioCreated { get; set; }
    }

    public class StudioListResponse
    {
        public List<StudioResponse> Studios { get; set; } = new List<StudioResponse>();
        public StudioListSubscriptionResponse Subscription { get; set; } = new StudioListSubscriptionResponse();
    }
}
