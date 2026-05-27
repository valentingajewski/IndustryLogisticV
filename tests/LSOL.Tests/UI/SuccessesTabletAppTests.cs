using System.Linq;
using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class SuccessesTabletAppTests
    {
        [TestMethod]
        public void BuildPage_RootIncludesCurrentDoctrinePrestigeAndSteeringSections()
        {
            var tracker = new PlayerSuccessTracker(null, null, null, null, null, null, null, null);
            var app = new SuccessesTabletApp(tracker);

            var page = app.BuildPage(null, new TabletRoute(TabletAppIds.Successes, "root"));
            var captions = page.Items.Select(GetCaption).ToList();

            CollectionAssert.Contains(captions, "Progress");
            CollectionAssert.Contains(captions, "Current Doctrine");
            CollectionAssert.Contains(captions, "Prestige");
            CollectionAssert.Contains(captions, "Steering");
            CollectionAssert.Contains(captions, "Regional Backbone");
            CollectionAssert.Contains(captions, "Integrated Chain");
            CollectionAssert.Contains(captions, "Client Priority");
        }

        [TestMethod]
        public void BuildPage_DoctrineRouteShowsComponentBreakdown()
        {
            var tracker = new PlayerSuccessTracker(null, null, null, null, null, null, null, null);
            var app = new SuccessesTabletApp(tracker);

            var page = app.BuildPage(null, new TabletRoute(TabletAppIds.Successes, "doctrine", CompanyDoctrine.Industrial));
            var captions = page.Items.Select(GetCaption).ToList();

            CollectionAssert.Contains(captions, "Status");
            CollectionAssert.Contains(captions, "Doctrine Effect");
            CollectionAssert.Contains(captions, "Lead Rule");
            CollectionAssert.Contains(captions, "Owned industries");
            CollectionAssert.Contains(captions, "Warehouses");
            CollectionAssert.Contains(captions, "Owned chain");
            CollectionAssert.Contains(captions, "Delivered commodities");
        }

        [TestMethod]
        public void BuildPage_PrestigeRouteShowsPrestigeBreakdownItems()
        {
            var tracker = new PlayerSuccessTracker(null, null, null, null, null, null, null, null);
            var app = new SuccessesTabletApp(tracker);

            var page = app.BuildPage(null, new TabletRoute(TabletAppIds.Successes, "prestige"));
            var captions = page.Items.Select(GetCaption).ToList();

            CollectionAssert.Contains(captions, "Prestige Total");
            CollectionAssert.Contains(captions, "Doctrine");
            CollectionAssert.Contains(captions, "Landmark HQ");
        }

        private static string GetCaption(MenuItem item)
        {
            return item != null && item.CaptionFactory != null
                ? item.CaptionFactory() ?? string.Empty
                : string.Empty;
        }
    }
}