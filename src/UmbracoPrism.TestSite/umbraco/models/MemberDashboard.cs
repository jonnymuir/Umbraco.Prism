using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;

namespace Umbraco.Cms.Web.Common.PublishedModels;

/// <summary>
/// Strongly-typed content model for the Member Dashboard document type.
/// Hand-authored to match the "memberDashboard" document type alias in Umbraco.
/// </summary>
[PublishedModel("memberDashboard")]
public partial class MemberDashboard : PublishedContentModel
{
    public const string ModelTypeAlias = "memberDashboard";
    public const PublishedItemType ModelItemType = PublishedItemType.Content;

    public MemberDashboard(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : base(content, publishedValueFallback)
    {
    }
}
