using Microsoft.Playwright;
using TUnit.Playwright;

namespace LegacyWeb.Playwright.Tests;

public abstract class LegacyWebPageTest(LegacyWebContainerFixture fixture) : PageTest
{
    protected LegacyWebContainerFixture App { get; } = fixture;

    public override string BrowserName => "chromium";

    protected async Task GoToAsync(string path = "/")
    {
        var address = new Uri(App.BaseAddress, path);
        await Page.GotoAsync(address.ToString());
    }
}

[ClassDataSource<LegacyWebContainerFixture>(Shared = SharedType.PerTestSession)]
public sealed class HomePageTests(LegacyWebContainerFixture fixture) : LegacyWebPageTest(fixture)
{
    [Test]
    public async Task Home_page_shows_the_original_sample_content()
    {
        await GoToAsync();

        await Assert.That(await Page.TitleAsync())
            .IsEqualTo("Home Page - My ASP.NET Application");
        await Assert.That(await Page.GetByRole(AriaRole.Heading, new() { Name = "ASP.NET" }).IsVisibleAsync())
            .IsTrue();
        await Assert.That(await Page.GetByRole(AriaRole.Heading, new() { Name = "Getting started" }).IsVisibleAsync())
            .IsTrue();
    }

    [Test]
    public async Task Existing_navigation_links_reach_their_pages()
    {
        await GoToAsync();

        await Assert.That(await Page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).IsVisibleAsync())
            .IsTrue();
        await Assert.That(await Page.GetByRole(AriaRole.Link, new() { Name = "About", Exact = true }).IsVisibleAsync())
            .IsTrue();
        await Assert.That(await Page.GetByRole(AriaRole.Link, new() { Name = "Contact", Exact = true }).IsVisibleAsync())
            .IsTrue();

        await Page.GetByRole(AriaRole.Link, new() { Name = "About", Exact = true }).ClickAsync();
        await Assert.That(await Page.GetByRole(AriaRole.Heading, new() { Name = "About." }).IsVisibleAsync())
            .IsTrue();

        await Page.GetByRole(AriaRole.Link, new() { Name = "Contact", Exact = true }).ClickAsync();
        await Assert.That(await Page.GetByRole(AriaRole.Heading, new() { Name = "Contact." }).IsVisibleAsync())
            .IsTrue();
    }

    [Test]
    public async Task About_page_contains_the_original_sample_content()
    {
        await GoToAsync("/About");

        await Assert.That(await Page.GetByRole(AriaRole.Heading, new() { Name = "About." }).IsVisibleAsync())
            .IsTrue();
    }

    [Test]
    public async Task Contact_page_contains_the_original_sample_content()
    {
        await GoToAsync("/Contact");

        await Assert.That(await Page.GetByRole(AriaRole.Heading, new() { Name = "Contact." }).IsVisibleAsync())
            .IsTrue();
    }

    [Test]
    public async Task Missing_page_returns_not_found()
    {
        var response = await Page.GotoAsync(new Uri(App.BaseAddress, "/page-that-does-not-exist").ToString());

        await Assert.That(response).IsNotNull();
        await Assert.That(response!.Status).IsEqualTo(404);
    }
}
