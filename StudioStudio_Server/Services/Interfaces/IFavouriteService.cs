using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho qu?n l? Favourites (groups yêu thích)
    /// </summary>
    public interface IFavouriteService
    {
        Task<FavouriteResponse> AddFavouriteAsync(Guid userId, AddFavouriteRequest request);
        Task RemoveFavouriteAsync(Guid userId, RemoveFavouriteRequest request);
    }
}
