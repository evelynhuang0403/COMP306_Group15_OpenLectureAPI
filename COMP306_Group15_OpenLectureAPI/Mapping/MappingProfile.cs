// Mapping/MappingProfile.cs
using AutoMapper;
using COMP306_Group15_OpenLectureAPI.Models;
using COMP306_Group15_OpenLectureAPI.Models.COMP306_Group15_OpenLectureAPI.Models;
using static COMP306_Group15_OpenLectureAPI.DTOs.CommentDtos;
using static COMP306_Group15_OpenLectureAPI.DTOs.PlaylistDtos;
using static COMP306_Group15_OpenLectureAPI.DTOs.ReactionDtos;
using static COMP306_Group15_OpenLectureAPI.DTOs.UserDtos;
using static COMP306_Group15_OpenLectureAPI.DTOs.VideoDtos;

namespace COMP306_Group15_OpenLectureAPI.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // ---- Users ----
            CreateMap<UserItem, UserReadDto>();
            CreateMap<UserCreateDto, UserItem>();
            CreateMap<UserUpdateDto, UserItem>()
                .ForAllMembers(opt => opt.Condition((src, dest, val) => val != null));

            // ---- Videos (UPDATED for Subject/CourseCode/Tags, no DurationSec) ----
            CreateMap<VideoItem, VideoReadDto>();

            CreateMap<VideoCreateDto, VideoItem>()
                .ForMember(d => d.UploadDate, cfg => cfg.MapFrom(_ => DateTime.UtcNow))
                .ForMember(d => d.UpdatedAt, cfg => cfg.Ignore())
                .ForMember(d => d.IsDeleted, cfg => cfg.MapFrom(_ => false))
                .ForMember(d => d.ViewCount, cfg => cfg.MapFrom(_ => 0))
                .ForMember(d => d.LikeCount, cfg => cfg.MapFrom(_ => 0))
                .ForMember(d => d.DislikeCount, cfg => cfg.MapFrom(_ => 0))
                .ForMember(d => d.CommentCount, cfg => cfg.MapFrom(_ => 0));

            CreateMap<VideoUpdateDto, VideoItem>()
                .ForMember(d => d.UpdatedAt, cfg => cfg.MapFrom(_ => DateTime.UtcNow));

            // Patches are applied manually in controller.

            // ---- Comments ----
            CreateMap<CommentItem, CommentReadDto>()
                .ForMember(d => d.IsOwnerReply, opt => opt.Ignore());
            CreateMap<CommentCreateDto, CommentItem>();
            CreateMap<CommentUpdateDto, CommentItem>()
                .ForAllMembers(opt => opt.Condition((src, dest, val) => val != null));

            // ---- Reactions ----
            CreateMap<ReactionItem, ReactionReadDto>();
            CreateMap<ReactionUpsertDto, ReactionItem>()
                .ForMember(d => d.ReactionId, opt => opt.MapFrom(s => $"{s.VideoId}|{s.UserId}"));

            // ---- Playlists ----
            CreateMap<PlaylistItem, PlaylistReadDto>()
                .ForMember(d => d.VideoIds,
                    cfg => cfg.MapFrom(s => (s.VideoIds ?? new HashSet<string>()).ToList())); // if the videoIds attribute is null, return an empty list to the client

            CreateMap<PlaylistCreateDto, PlaylistItem>()
                .ForMember(d => d.PlaylistId, cfg => cfg.Ignore())
                .ForMember(d => d.VideoIds,
                    cfg => cfg.MapFrom(s => (s.VideoIds ?? Array.Empty<string>())
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase)));

            CreateMap<PlaylistUpdateDto, PlaylistItem>()
                .ForMember(d => d.VideoIds,
                    cfg => cfg.MapFrom(s => (s.VideoIds ?? Array.Empty<string>())
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase)))
                .ForAllMembers(opt => opt.Condition((src, dest, val) => val != null));
        }
    }
}
