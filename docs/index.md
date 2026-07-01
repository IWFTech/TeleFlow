---
title: TeleFlow
hide:
  - navigation
  - toc
---

<section class="tf-hero">
  <div class="tf-hero__content">
    <p class="tf-kicker">Telegram bot framework for .NET</p>
    <h1>Starts like a script.<br />Grows like a system.</h1>
    <p class="tf-lead">
      TeleFlow keeps the first command simple and the grown bot explicit:
      typed handlers, dependency injection, generated metadata, state,
      callbacks, long polling, webhooks, and direct Telegram Bot API access.
    </p>
    <div class="tf-actions">
      <a class="md-button md-button--primary" href="en/getting-started/quickstart/">Get started</a>
      <a class="md-button" href="ru/getting-started/quickstart/">Начать на русском</a>
      <a class="md-button" href="https://github.com/IWFTech/TeleFlow">GitHub</a>
    </div>
  </div>
  <div class="tf-terminal" aria-label="TeleFlow quickstart code">
    <div class="tf-terminal__bar">
      <span></span><span></span><span></span>
      <strong>Program.cs</strong>
    </div>
    <div class="tf-terminal__code" markdown="1">

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
});

builder.Services.AddLongPolling();
builder.Services.AddTelegramHandlersFromAssembly(
    typeof(Program).Assembly);

public sealed class StartHandler
{
    [Command("start")]
    public Task Handle(
        MessageContext ctx,
        CancellationToken ct)
    {
        return ctx.Message.AnswerAsync(
            "Hello from TeleFlow.");
    }
}
```

    </div>
  </div>
</section>

<section class="tf-strip" aria-label="Project links">
  <a href="https://www.nuget.org/packages/IWF.TeleFlow.Framework.LongPolling/">NuGet packages</a>
  <a href="https://t.me/teleflow_chat">Telegram chat</a>
  <a href="en/enterprise/architecture/">Architecture</a>
  <a href="en/roadmap/">Roadmap</a>
</section>

<section class="tf-section">
  <div class="tf-section__heading">
    <p class="tf-kicker">Choose your path</p>
    <h2>One documentation site for different levels</h2>
  </div>
  <div class="tf-card-grid">
    <a class="tf-card" href="en/getting-started/quickstart/">
      <span class="tf-card__icon">01</span>
      <h3>First bot</h3>
      <p>Build a minimal long polling bot, add handlers, and understand the basic TeleFlow application model.</p>
    </a>
    <a class="tf-card" href="en/tutorials/support-desk/">
      <span class="tf-card__icon">02</span>
      <h3>Real workflow</h3>
      <p>Walk through a support desk bot with state, callbacks, DI services, and admin actions.</p>
    </a>
    <a class="tf-card" href="en/enterprise/">
      <span class="tf-card__icon">03</span>
      <h3>Enterprise shape</h3>
      <p>Review architecture, deployment, versioning, diagnostics, performance, and production checklist notes.</p>
    </a>
  </div>
</section>

<section class="tf-section tf-section--split">
  <div>
    <p class="tf-kicker">Framework principles</p>
    <h2>Convenient does not have to mean hidden</h2>
    <p>
      TeleFlow uses normal C# handlers and normal .NET dependency injection.
      The recommended path generates handler metadata at build time and fails
      clearly when generated registration is missing.
    </p>
  </div>
  <div class="tf-feature-list">
    <a href="en/advanced/generated-registration/">Generated handler registration</a>
    <a href="en/features/telegram-client/">Direct Telegram Bot API client</a>
    <a href="en/features/state-and-wizard/">State and wizard flows</a>
    <a href="en/features/errors-and-diagnostics/">Errors and diagnostics</a>
  </div>
</section>

<section class="tf-section">
  <div class="tf-section__heading">
    <p class="tf-kicker">Documentation map</p>
    <h2>Read in English or Russian</h2>
  </div>
  <div class="tf-card-grid tf-card-grid--compact">
    <a class="tf-card" href="en/">
      <h3>English documentation</h3>
      <p>Getting started, fundamentals, features, transports, advanced topics, enterprise notes, and reference.</p>
    </a>
    <a class="tf-card" href="ru/">
      <h3>Русская документация</h3>
      <p>Те же разделы на русском: старт, routing, callbacks, state, transports, enterprise и roadmap.</p>
    </a>
  </div>
</section>
