﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StoredObjects;
using WebApiResources;

namespace Services
{
    public interface IOrganisationServiceDependencies
    {
        IUserService UserService { get; set; }
        IStorageService StorageService { get; set; }
        ILinqService LinqService { get; set; }
        IUrlSlugService UrlSlugService { get; set; }
    }
    public class OrganisationServiceDependencies : IOrganisationServiceDependencies
    {
        public IUserService UserService { get; set; }
        public IStorageService StorageService { get; set; }
        public ILinqService LinqService { get; set; }
        public IUrlSlugService UrlSlugService { get; set; }

        public OrganisationServiceDependencies(IUserService userService,
            IStorageService storageService,
            ILinqService linqService,
            IUrlSlugService urlSlugService)
        {
            UserService = userService;
            StorageService = storageService;
            LinqService = linqService;
            UrlSlugService = urlSlugService;
        }
    }
    public interface IOrganisationService
    {
        OrganisationResource GetRootOrganisation(IPrincipal principal);
        ShurahBasedOrganisation GetOrganisation(int organisationId);
        List<string> GetMemberPermissions(Auth0User user, ShurahBasedOrganisation organisation);
        List<string> GetMemberPermissions(Auth0User user, MembershipRuleSection section);
        List<string> GetMemberPermissions(Auth0User user, MembershipRule rule);

        List<string> GetPermission(IPrincipal principal, int organisationId);
        void GuaranteeUserHasPermission(Auth0User user, ShurahBasedOrganisation organisation,
            ShurahOrganisationPermission permission);

        TermDefinitionResource GetTermDefinition(int termId,int organisationId);
    }
    public class OrganisationService: IOrganisationService
    {
        private readonly IOrganisationServiceDependencies _dependencies;

        public OrganisationService(IOrganisationServiceDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public virtual OrganisationResource GetRootOrganisation(IPrincipal principal)
        {
            var org = _dependencies.LinqService.Single(
              _dependencies.StorageService.SetOf<ShurahBasedOrganisation>(),
              o => o.ParentOrganisationRelationship == null);
            var user = _dependencies.UserService.GetAuthenticatedUser(principal);
            return BuildOrganisationResource(org, user);
        }

        public ShurahBasedOrganisation GetOrganisation(int organisationId)
        {
            return _dependencies.StorageService.SetOf<ShurahBasedOrganisation>().Single(x => x.Id == organisationId);
        }

        public virtual OrganisationResource BuildOrganisationResource(ShurahBasedOrganisation organisation, Auth0User user)
        {
            var resource = new OrganisationResource();
            resource.Name = organisation.Name;
            resource.JoiningPolicy = organisation.JoiningPolicy.ToString();
            resource.Description = organisation.Description;
            resource.Member= GetMember(user, organisation.Members);
            resource.RuleSections = BuildMembershipRuleSectionResources(string.Empty
                ,_dependencies.LinqService.Where(organisation.MembershipRuleSections,r=>r.ParentMembershipRuleSection==null)
                ,SortTerms(organisation.Terms),user);
            resource.Permissions = GetMemberPermissions(user, organisation);
            return resource;
        }

        public virtual List<string> GetMemberPermissions(Auth0User user, ShurahBasedOrganisation organisation)
        {
            if (user == null || organisation==null)
            {
                return new List<string>();
            }
            var memberAuthUser = user.MemberAuth0Users.FirstOrDefault(m => m.Member.OrganisationId == organisation.Id && !m.Member.Removed);
            if (memberAuthUser == null)
            {
                return new List<string>();
            }
            var permissions = memberAuthUser.Member.DelegatedPermissions.Select(x => x.ShurahOrganisationPermission).Distinct();

            if (organisation.OrganisationLeader != null &&
                organisation.OrganisationLeader.LeaderMemberId == memberAuthUser.MemberId)
            {
                permissions = Enum.GetValues(typeof (ShurahOrganisationPermission)).OfType<ShurahOrganisationPermission>().ToList();
            }
            return permissions.Select(p => p.ToString()).ToList();
        }

        public virtual List<string> GetMemberPermissions(Auth0User user, MembershipRuleSection section)
        {
            if (section == null)
            {
                return new List<string>();
            }
            return GetMemberPermissions(user,section.ShurahBasedOrganisation);
        }

        public List<string> GetMemberPermissions(Auth0User user, MembershipRule rule)
        {
            if (rule == null)
            {
                return new List<string>();
            }
            return GetMemberPermissions(user,rule.MembershipRuleSection);
        }

        public List<string> GetPermission(IPrincipal principal, int organisationId)
        {
            var user = _dependencies.UserService.GetAuthenticatedUser(principal);
            return GetMemberPermissions(user,_dependencies.StorageService.SetOf<ShurahBasedOrganisation>().FirstOrDefault(
                x=>x.Id==organisationId));
        }

        public void GuaranteeUserHasPermission(Auth0User user, ShurahBasedOrganisation organisation,
            ShurahOrganisationPermission permission)
        {
            var permissions = GetMemberPermissions(user, organisation);
            if (!permissions.Contains(permission.ToString()))
            {
                throw new Exception("Access Denied");
            }
        }

        public TermDefinitionResource GetTermDefinition(int termId,int organisationId)
        {
            var organisation =
                _dependencies.StorageService.SetOf<ShurahBasedOrganisation>()
                    .FirstOrDefault(x => x.Id == organisationId);
            if (organisation == null)
            {
                return new TermDefinitionResource
                {
                    HasError = true,
                    Error = "Organisation not Found"
                };
            }
            var term =
                organisation.Terms.FirstOrDefault(x => x.Id == termId);
            if (term == null)
            {
                return new TermDefinitionResource
                {
                    HasError = true,
                    Error = "Term not Found"
                };
            }
            return new TermDefinitionResource
            {
                Term = term.Term,
                Id=term.Id,
                Definition= ParseRuleStatement(term.Definition, organisation.Terms)
            };
        }


        public virtual List<MembershipRuleSectionResource> BuildMembershipRuleSectionResources(string sectionPrefix,
            IEnumerable<MembershipRuleSection> ruleSections, IEnumerable<MembershipRuleTermDefinition> terms, Auth0User user)
        {
            var orderedSections = _dependencies.LinqService.OrderBy(ruleSections, x => x.Sequence);
            var resources = _dependencies.LinqService.SelectIndexedEnumerable(orderedSections,
                (s, i) => BuildMembershipRuleSectionResource(sectionPrefix, s, user, i,terms));
            return _dependencies.LinqService.EnumerableToList(resources);
        }

        public virtual MembershipRuleSectionResource BuildMembershipRuleSectionResource(string sectionPrefix, 
            MembershipRuleSection ruleSection, Auth0User user, int sectionIndex, IEnumerable<MembershipRuleTermDefinition> terms)
        {
            var resource = new MembershipRuleSectionResource();
            resource.Title = ruleSection.Title;
            resource.Id = ruleSection.Id;
            resource.SectionNumber = sectionPrefix + (sectionIndex + 1);
            resource.UniqueName = ruleSection.UniqueInOrganisationName;
            var orderedRules = _dependencies.LinqService.OrderBy(ruleSection.MembershipRules, r => r.Sequence);
            var prefix = sectionPrefix + (sectionIndex + 1).ToString() + ".";
            resource.Rules =
                _dependencies.LinqService.EnumerableToList(
                    _dependencies.LinqService.SelectIndexedEnumerable(orderedRules,
                        (r, i) => BuildMembershipRuleResource(prefix, user, r, i, terms)));
            resource.SubSections = BuildMembershipRuleSectionResources(
                prefix,
                _dependencies.LinqService.SelectEnumerable(ruleSection.ChildMembershipRuleSections,
                    x => x.MembershipRuleSection), terms, user);
            return resource;
        }

        public virtual MembershipRuleResource BuildMembershipRuleResource(string rulePrefix, Auth0User user, MembershipRule rule, int ruleIndex, IEnumerable<MembershipRuleTermDefinition> terms)
        {
            var resource = new MembershipRuleResource();
            resource.Id = rule.Id;
            resource.Number = rulePrefix + (ruleIndex+1);
            resource.RuleFragments = ParseRuleStatement(rule.RuleStatement, terms);
            resource.ComprehensionScore = GetComprehensionScore(rule, user);
            resource.MaxComprehensionScore =
                _dependencies.LinqService.EnumerableCount(rule.MembershipRuleComprehensionQuestions);
            resource.PublishedUtcDateTimeText = rule.PublishedDateTimeUtc.ToString("s");
            resource.RuleStatement = rule.RuleStatement;
            return resource;
        }

        public virtual MemberResource GetMember(Auth0User user, IEnumerable<Member> members)
        {
            if (user == null)
            {
                return null;
            }
            var member= _dependencies.LinqService.SingleOrDefault(members,m => m.MemberAuth0Users.Any(x => x.Auth0UserId == user.Id));
            if (member == null || member.Removed)
            {
                return null;
            }
            return BuildMemberResource(member);
        }

        public virtual MemberResource BuildMemberResource(Member member)
        {
            var resource = new MemberResource();
            resource.Id = member.Id;
            resource.DirectFollowers = _dependencies.LinqService.EnumerableCount(member.Followers);
            resource.IndirectFollowers = _dependencies.LinqService.EnumerableSum(member.Followers, f => f.FolloweCount);
            resource.ToDoCount = GetPendingActionsCount(member.ActionUpdates);
            resource.LeaderPublicName = member?.LeaderRecognition?.RecognisedLeaderMember?.PublicName;
            resource.PublicName = member.PublicName;
            return resource;
        }

        public virtual List<TextFragmentResource> ParseRuleStatement(string ruleStatement, IEnumerable<MembershipRuleTermDefinition> terms)
        {
            var fragments = CreateNewFragmentList(ruleStatement);
            fragments=_dependencies.LinqService.Aggregate(terms,fragments, ApplyTerm);
            return _dependencies.LinqService.EnumerableToList(
                _dependencies.LinqService.SelectMany(fragments, AddQuranReferences));

        }
        public static Regex QuranRefRegex=new Regex("Q([1-9]{1,3}):([0-9]{1,3})");
        public virtual IEnumerable<TextFragmentResource> AddQuranReferences(TextFragmentResource textFragmentResource)
        {
            if (!textFragmentResource.IsPlainText)
            {
                return new List<TextFragmentResource>() {textFragmentResource};
            }
            return ParseForQuranReferences(textFragmentResource.Text);
        }

        public virtual IEnumerable<TextFragmentResource> ParseForQuranReferences(string text)
        {
            var matches = QuranRefRegex.Matches(text);
            var fragments = new List<TextFragmentResource>();
            var currentTextIndex = 0;
            for(var i=0;i<matches.Count;i++)
            {
                var match = matches[i];
                if (match.Groups.Count == 3)
                {
                    fragments.Add(new TextFragmentResource { IsPlainText = true, Text = text.Substring(currentTextIndex, match.Index - currentTextIndex) });
                    fragments.Add(new TextFragmentResource
                    {

                        QuranReference = new QuranReferenceResource
                        {
                            Surah = int.Parse(match.Groups[1].Value),
                            Verse = int.Parse(match.Groups[2].Value)
                        }
                    });
                    currentTextIndex = match.Index + match.Length;

                }

            }
            var lastFragment = new TextFragmentResource {IsPlainText = true, Text = text.Substring(currentTextIndex)};
            if (!string.IsNullOrEmpty(lastFragment.Text))
            {
                fragments.Add(lastFragment);
            }
            return fragments;
        }

        public virtual List<TextFragmentResource> ApplyTerm(List<TextFragmentResource> fragments, MembershipRuleTermDefinition term)
        {
            return _dependencies.LinqService.EnumerableToList(
                _dependencies.LinqService.SelectMany(fragments,f => Split(f, term)));
        }
        public virtual List<TextFragmentResource> Split(TextFragmentResource fragment, MembershipRuleTermDefinition term)
        {
            if (!fragment.IsPlainText)
            {
                return new List<TextFragmentResource> {fragment};
            }
            var indexOfMatch = fragment.Text.ToLower().IndexOf(term.Term.ToLower(), StringComparison.Ordinal);
            if (indexOfMatch == -1)
            {
                return new List<TextFragmentResource> {fragment};
            }
   
            var results = new List<TextFragmentResource>();
            var textBefore = fragment.Text.Substring(0, indexOfMatch);
            if (textBefore != string.Empty)
            {
                results.Add(new TextFragmentResource
                {
                    IsPlainText = true,
                    Text = textBefore
                });
            }
            results.Add(new TextFragmentResource
            {
                IsPlainText = false,
                IsTerm = true,
                Text = term.Term,
                TermSlug = _dependencies.UrlSlugService.GetSlug(term.Term),
                TermId = term.Id
            });
            var textAfter = fragment.Text.Substring(indexOfMatch + term.Term.Length);
            if (textAfter != string.Empty)
            {
                results.Add(new TextFragmentResource
                {
                    IsPlainText = true,
                    Text = textAfter
                });
            }
            return results;
        }

        public virtual int GetComprehensionScore(MembershipRule rule, Auth0User user)
        {
            if (user == null)
            {
                return 0;
            }
            var memberLink =
               user.MemberAuth0Users
                    .SingleOrDefault(
                        m => m.Member.OrganisationId == rule.MembershipRuleSection.ShurahBasedOrganisationId);
            var users = new List<Auth0User> {user};
            if (memberLink != null)
            {
                users.AddRange(memberLink.Member.MemberAuth0Users.Select(m => m.Auth0User));
            }
            return users.SelectMany(u => u.MembershipRuleComprehensionTestResults)
                .Where(t => t.MembershipRuleComprehensionQuestion.MembershipRuleId == rule.Id)
                .Where(t=>t.CorrectlyAnswered)
                .Where(t=>t.AnsweredDateTimeUtc>rule.PublishedDateTimeUtc)
                .GroupBy(x=>x.MembershipRuleComprehensionQuestionId).Count();


            //var correctAnswers=_dependencies.StorageService.SetOf<MembershipRuleComprehensionTestResult>()
            //    .Where(r => r.MembershipRuleComprehensionQuestion.MembershipRuleId == rule.Id
            //                && ids.Contains(r.Auuth0UserId)
            //                && r.CorrectlyAnswered
            //                && r.StartedDateTimeUtc>rule.PublishedDateTimeUtc)
            //                .GroupBy(x=>x.MembershipRuleComprehensionQuestionId).Count();
            //return correctAnswers;
        }

        public virtual int GetPendingActionsCount(IEnumerable<ActionUpdate> actionUpdates)
        {
            return actionUpdates.Count(a => a.Status != ActionStatus.Completed || a.Status != ActionStatus.Abandoned);
        }

        public virtual List<MembershipRuleTermDefinition> SortTerms(IEnumerable<MembershipRuleTermDefinition> terms)
        {
            var sort1 = _dependencies.LinqService.OrderBy(terms, t => t.Term.Length);
            var sort2 = _dependencies.LinqService.ThenBy(sort1, t => t.Term);
            return _dependencies.LinqService.EnumerableToList(sort2);
        }

        public virtual List<TextFragmentResource> CreateNewFragmentList(string statement)
        {
            return new List<TextFragmentResource> {new TextFragmentResource {IsPlainText = true,Text = statement} };
        }
    }
}
