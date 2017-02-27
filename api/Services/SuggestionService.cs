﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using StoredObjects;
using WebApiResources;

namespace Services
{
    public interface ISuggestionServiceDependencies
    {
        IStorageService StorageService { get; set; }
        IUserService UserService { get; set; }
        IOrganisationService OrganisationService { get; set; }
    }
    public class SuggestionServiceDependencies: ISuggestionServiceDependencies
    {
        public IOrganisationService OrganisationService { get; set; }
        public IUserService UserService { get; set; }
        public IStorageService StorageService { get; set; }

        public SuggestionServiceDependencies(IOrganisationService organisationService,
            IUserService userService,
            IStorageService storageService
            )
        {
            OrganisationService = organisationService;
            UserService = userService;
            StorageService = storageService;
        }
    }
    public interface ISuggestionService
    {
        ResponseResource CreateSuggestion(IPrincipal principal, CreateSugestionRequest request);
        ResponseResource DeleteSuggestion(IPrincipal principal, DeleteSugestionRequest request);
        SearchSugestionsResponse Search(IPrincipal principal, SearchSuggestionsRequest request);
        SuggestionDetailResource ViewSuggestion(IPrincipal user, ViewSugestionRequest request);
    }
    public class SuggestionService: ISuggestionService
    {
        private readonly ISuggestionServiceDependencies _dependencies;
        public SuggestionService(ISuggestionServiceDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public ResponseResource CreateSuggestion(IPrincipal principal, CreateSugestionRequest request)
        {
            var organisationId = request.OrganisationId;
            var member = GetGuaranteedMember(principal, organisationId);

            var suggestion = _dependencies.StorageService.SetOf<Suggestion>().Create();
            suggestion.AuthorMember = member;
            suggestion.AuthorMemberId = member.Id;
            suggestion.FullText = request.Suggestion;
            suggestion.ShortDescription = request.Subject;
            suggestion.CreatedDateUtc = DateTime.UtcNow;

            _dependencies.StorageService.SetOf<Suggestion>().Add(suggestion);
            _dependencies.StorageService.SaveChanges();
            return new ResponseResource();
        }

        private Member GetGuaranteedMember(IPrincipal principal, int organisationId)
        {
            var user = _dependencies.UserService.GetAuthenticatedUser(principal);
            if (user == null || user.MemberAuth0Users.All(m => m.Member.OrganisationId != organisationId))
            {
                throw new Exception("Access Denied");
            }
            var member = user.MemberAuth0Users.First(m => m.Member.OrganisationId == organisationId && !m.Member.Removed).Member;
            return member;
        }

        public SearchSugestionsResponse Search(IPrincipal principal, SearchSuggestionsRequest request)
        {
            var member = GetGuaranteedMember(principal, request.OrganisationId);

            var suggestionsQuery = member.Organisation.Members
                .SelectMany(m => m.Suggestions)
                .Where(x => !x.AuthorMember.Removed);
            var suggestions = suggestionsQuery
                .OrderByDescending(s => s.CreatedDateUtc)
                .Skip(((request.Page ?? 1) - 1)*100).Take(100);
            
            return new SearchSugestionsResponse
            {
                OrganisationId = request.OrganisationId,
                PageCount = suggestionsQuery.Count()/100,
                Suggestions = suggestions.Select(BuildSummarySuggestion).ToList()
            };
                
            throw new NotImplementedException();
        }

        public virtual SuggestionSummaryResource BuildSummarySuggestion(Suggestion suggestion)
        {
            return new SuggestionSummaryResource
            {
                Id = suggestion.Id,
                Subject = suggestion.ShortDescription,
                DateTimeText = suggestion.CreatedDateUtc.ToString("s"),
                NetSupportPercent = GetPercentSupport(suggestion),
                VotingPercent = GetVotingPercent(suggestion),
                AuthorMemberId = suggestion.AuthorMemberId,
                AuthorPublicName = suggestion.AuthorMember.PublicName,
                AuthorPictureUrl = suggestion.AuthorMember.MemberAuth0Users.First().Auth0User.PictureUrl
            };
         //   throw new NotImplementedException();
        }

        public double GetVotingPercent(Suggestion suggestion)
        {
            var support = GetVoteCount(suggestion, true);
            var opposition = GetVoteCount(suggestion, false);
            return (100.0 * (support+opposition+GetAbstentionCount(suggestion))) / 
                (suggestion.AuthorMember.Organisation.Members.Where(m=>!m.Removed).Count());
            throw new NotImplementedException();
        }

        public virtual double GetPercentSupport(Suggestion suggestion)
        {
            var support = GetVoteCount(suggestion, true);
            var opposition = GetVoteCount(suggestion, false);
            if ((support + opposition) == 0) { return 0; }
            return Math.Round( (100.0 * (support-opposition)) / (support + opposition),1);
            throw new NotImplementedException();
        }

        public ResponseResource DeleteSuggestion(IPrincipal principal, DeleteSugestionRequest request)
        {
            var user = _dependencies.UserService.GetGuaranteedAuthenticatedUser(principal);
            var suggestion = _dependencies.StorageService.SetOf<Suggestion>().SingleOrDefault(s => s.Id == request.SuggestionId);
            if (suggestion == null)
            {
                throw new Exception("Suggestion not found");
            }
            var member = GetGuaranteedMember(principal, suggestion.AuthorMember.OrganisationId);
            if(suggestion.AuthorMemberId != member.Id)
            {
                _dependencies.OrganisationService.GuaranteeUserHasPermission(user, member.Organisation, ShurahOrganisationPermission.RemoveSuggestion);
            }
            _dependencies.StorageService.SetOf<Suggestion>().Remove(suggestion);
            _dependencies.StorageService.SaveChanges();
            return new ResponseResource();
        }

        public virtual SuggestionDetailResource ViewSuggestion(IPrincipal principal, ViewSugestionRequest request)
        {
            var user = _dependencies.UserService.GetGuaranteedAuthenticatedUser(principal);
            var suggestion = _dependencies.StorageService.SetOf<Suggestion>().FirstOrDefault(s =>
                s.AuthorMember.Organisation.Members.Any(m => m.MemberAuth0Users.Any(a => a.Auth0UserId == user.Id))
                && s.Id == request.SuggestionId);
            if (suggestion == null)
            {
                return new SuggestionDetailResource { HasError = true, Error = "Suggestion not found." };
            }
            var member = GetGuaranteedMember(principal, suggestion.AuthorMember.OrganisationId);
            var vote = suggestion.SuggestionVotes.SingleOrDefault(v => v.VoterMemberId == member.Id);
            return new SuggestionDetailResource
            {
                Suggestion = suggestion.FullText,
                SuggestionSummary = BuildSummarySuggestion(suggestion),
                UserVoteId = vote?.Id,
                UserVoteIsSupporting = vote?.MemberIsSupportingSuggestion,
                VotesFor = GetVoteCount(suggestion,true),
                VotesAgainst = GetVoteCount(suggestion,false),
                AbstentionCount = GetAbstentionCount(suggestion)
            };

            throw new NotImplementedException();
        }
        public virtual int GetVoteCount(Suggestion suggestion,bool inFavour)
        {
            return suggestion.SuggestionVotes.Where(v => 
            !v.VoterMember.Removed
            && v.MemberIsSupportingSuggestion.HasValue 
            && v.MemberIsSupportingSuggestion.Value==inFavour)
                .Sum(v => 1 + v.DelegatedVoteCount);
        }
        public virtual int GetAbstentionCount(Suggestion suggestion)
        {
            return suggestion.SuggestionVotes.Where(
                v => 
                !v.MemberIsSupportingSuggestion.HasValue
                && !v.VoterMember.Removed)
                .Sum(v => 1 + v.DelegatedVoteCount);
        }
    }
}
