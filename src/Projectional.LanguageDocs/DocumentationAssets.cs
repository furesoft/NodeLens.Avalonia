namespace Projectional.LanguageDocs;

/// <summary>The one stylesheet and the one script every page uses.</summary>
/// <remarks>
/// Both are inlined here rather than shipped as files in this package, because the output has to be a folder that works
/// from <c>file://</c>, from a web server and inside a zip: three files that exist only in the tree are the only ones
/// the pages can count on. The script is small on purpose - a filter over the tree and a drawer for narrow screens, and
/// a static page whose script does more is a static page with a bug. Nothing on the page needs the script to be
/// readable: the tree is built from <c>details</c>, which the browser folds by itself.
/// </remarks>
internal static class DocumentationAssets
{
    /// <summary>The stylesheet, which is deliberately readable and has no external dependency.</summary>
    internal const string Css = """
        :root {
          color-scheme: light dark;
          --ink: #1d1d1f; --muted: #6b6b70; --line: #dcdce1; --accent: #0b62d6;
          --bg: #ffffff; --panel: #fafafc; --hover: #eeeef3; --mark: #dde9fb;
          --top: 3.1rem; --side: 17rem;
        }
        @media (prefers-color-scheme: dark) {
          :root { --ink: #e8e8ea; --muted: #9a9aa2; --line: #35353c; --accent: #6fb0ff;
                  --bg: #16161a; --panel: #1b1b20; --hover: #26262d; --mark: #24344b; }
        }
        * { box-sizing: border-box; }
        html { scroll-padding-top: var(--top); }
        body { margin: 0; background: var(--bg); color: var(--ink);
               font: 15px/1.55 system-ui, -apple-system, "Segoe UI", sans-serif; }
        a { color: var(--accent); }

        /* the header: brand, one search, and the toggle a phone needs */
        header.top { position: sticky; top: 0; z-index: 6; display: flex; align-items: center; gap: .6rem;
                     height: var(--top); padding: 0 .8rem; background: var(--bg); border-bottom: 1px solid var(--line); }
        header.top .brand { font-weight: 650; color: var(--ink); text-decoration: none; white-space: nowrap;
                            overflow: hidden; text-overflow: ellipsis; }
        header.top input[type="search"] { margin-left: auto; width: min(24rem, 42vw); padding: .35rem .6rem;
                                          font: inherit; color: inherit; background: var(--panel);
                                          border: 1px solid var(--line); border-radius: .35rem; }
        header.top input[type="search"]:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }
        .menu { display: none; align-items: center; justify-content: center; width: 2rem; height: 2rem; padding: 0;
                color: inherit; background: transparent; border: 0; border-radius: .3rem; cursor: pointer; }
        .menu:hover { background: var(--hover); }

        /* the tree and the content side by side, the tree scrolling on its own */
        .shell { display: grid; grid-template-columns: var(--side) minmax(0, 1fr); align-items: start; }
        nav.tree { position: sticky; top: var(--top); min-height: calc(100vh - var(--top));
                   max-height: calc(100vh - var(--top)); overflow: auto;
                   padding: .5rem .35rem 2rem; background: var(--panel); border-right: 1px solid var(--line);
                   font-size: .9rem; }
        nav.tree details { margin: 0; }
        nav.tree summary { display: flex; gap: .4rem; align-items: center; padding: .28rem .45rem;
                           border-radius: .3rem; cursor: pointer; list-style: none; }
        nav.tree summary:hover { background: var(--hover); }
        /* The native marker is gone the moment a summary is a flex box, so the chevron is drawn: a right corner turned
           into an arrow, pointing right while folded and down while open. */
        nav.tree summary::-webkit-details-marker { display: none; }
        nav.tree summary::before { content: ""; flex: 0 0 auto; width: .38rem; height: .38rem;
                                   border-right: 1.6px solid var(--muted); border-bottom: 1.6px solid var(--muted);
                                   transform: rotate(-45deg); transition: transform .12s ease; }
        nav.tree details[open] > summary::before { transform: rotate(45deg); }
        nav.tree > details > summary { font-weight: 650; }
        nav.tree details.folder { margin-left: .9rem; }
        nav.tree a.item { display: block; padding: .2rem .45rem .2rem 1.25rem; border-radius: .3rem;
                          color: var(--ink); text-decoration: none; white-space: nowrap; overflow: hidden;
                          text-overflow: ellipsis; }
        nav.tree a.item:hover { background: var(--hover); }
        nav.tree a.item.current { background: var(--mark); font-weight: 650; }
        nav.tree .count { color: var(--muted); font-weight: 400; font-size: .82em; }
        /* Specific enough to beat `nav.tree a.item`, which sets a display of its own: a filter whose rule loses on
           specificity hides nothing and shows everything, and it looks like a script bug. */
        nav.tree a.item.hidden, nav.tree details.hidden { display: none; }

        /* the content */
        main { padding: 1.1rem 2rem 4rem; max-width: 62rem; min-width: 0; }
        h1 { font-size: 1.55rem; margin: .2rem 0 1rem; }
        h2 { font-size: 1.05rem; margin: 1.9rem 0 .5rem; padding-bottom: .25rem; border-bottom: 1px solid var(--line); }
        h3 { font-size: .95rem; margin: 1.2rem 0 .3rem; }
        nav.breadcrumb { font-size: .82rem; color: var(--muted); margin: 0 0 .5rem; }
        nav.breadcrumb a { color: var(--accent); text-decoration: none; }
        nav.breadcrumb a:hover { text-decoration: underline; }
        .tw { overflow-x: auto; }
        table { border-collapse: collapse; width: 100%; margin: .4rem 0 1rem; font-size: .9rem; }
        th, td { text-align: left; vertical-align: top; padding: .32rem .7rem .32rem 0; border-bottom: 1px solid var(--line); }
        th { color: var(--muted); font-weight: 600; white-space: nowrap; }
        code, .key { font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: .88em; }
        .muted { color: var(--muted); }
        .empty { color: var(--muted); font-style: italic; }
        .unresolved { color: var(--muted); text-decoration: line-through dotted; }
        .mismatch { color: #b3401b; }
        @media (prefers-color-scheme: dark) { .mismatch { color: #ff9a76; } }
        .backdrop { position: fixed; inset: var(--top) 0 0; z-index: 5; background: rgb(0 0 0 / 38%); }

        /* a phone: the tree becomes a drawer the header's toggle slides in, and the content takes the whole width */
        @media (max-width: 820px) {
          .menu { display: inline-flex; }
          header.top { padding: 0 .6rem; }
          header.top .brand { flex: 1 1 auto; }
          header.top input[type="search"] { margin-left: 0; flex: 0 1 11rem; width: auto; }
          .shell { display: block; }
          /* The drawer starts under the header rather than over it: the toggle that opened it is then still the toggle
             that closes it, and the search stays reachable while the tree is open. */
          nav.tree { position: fixed; z-index: 7; top: var(--top); bottom: 0; left: 0; width: 82vw; max-width: 20rem;
                     min-height: 0; max-height: none; padding-top: .5rem; border-right: 1px solid var(--line);
                     box-shadow: 0 0 2rem rgb(0 0 0 / 25%); transform: translateX(-102%);
                     transition: transform .18s ease; }
          body.nav-open nav.tree { transform: none; }
          main { padding: .9rem .9rem 3rem; }
          h1 { font-size: 1.3rem; }
        }
        @media (prefers-reduced-motion: reduce) { nav.tree { transition: none; } }
        """;

    /// <summary>The script: filter the tree, and open it as a drawer on a narrow screen. Nothing else.</summary>
    internal const string Js = """
        // The whole of the interactivity: the search box hides the tree entries that do not match, and on a narrow screen
        // the header's toggle slides the tree in. Nothing here needs the network, so the folder works from file://, from
        // a server and inside a zip - and the tree is folded by the browser itself, so a page without this script is a
        // page a reader can still walk.
        (function () {
          'use strict';

          function drawer() {
            var body = document.body;
            var toggle = document.querySelector('.menu');
            var backdrop = document.querySelector('.backdrop');
            if (!toggle) { return; }

            function show(open) {
              body.classList.toggle('nav-open', open);
              toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
              if (backdrop) { backdrop.hidden = !open; }
            }

            toggle.addEventListener('click', function () { show(!body.classList.contains('nav-open')); });
            if (backdrop) { backdrop.addEventListener('click', function () { show(false); }); }
            document.addEventListener('keydown', function (event) {
              if (event.key === 'Escape') { show(false); }
            });
            document.querySelectorAll('nav.tree a.item').forEach(function (link) {
              link.addEventListener('click', function () { show(false); });
            });
          }

          function filter() {
            var box = document.querySelector('input[type="search"]');
            var tree = document.querySelector('nav.tree');
            if (!box || !tree) { return; }

            var groups = Array.prototype.slice.call(tree.querySelectorAll('details'));
            var items = Array.prototype.slice.call(tree.querySelectorAll('a.item'));

            // What the reader had folded before typing, so clearing the box puts the tree back instead of leaving every
            // branch a search happened to open.
            var folded = null;

            function summaryOf(group) {
              var summary = group.firstElementChild;
              return summary && summary.tagName === 'SUMMARY' ? summary.textContent.toLowerCase() : '';
            }

            function show(item, yes) {
              item.classList.toggle('hidden', !yes);
            }

            function apply() {
              var needle = box.value.trim().toLowerCase();

              if (folded === null) {
                folded = groups.map(function (group) { return group.open; });
              }

              if (needle === '') {
                items.forEach(function (item) { show(item, true); });
                groups.forEach(function (group, index) {
                  group.classList.remove('hidden');
                  group.open = folded[index];
                });
                document.body.classList.remove('filtering');
                folded = null;
                return;
              }

              items.forEach(function (item) {
                show(item, (item.textContent || '').toLowerCase().indexOf(needle) >= 0);
              });

              // Innermost first, so a branch that keeps something is decided before the branch that holds it. A branch
              // whose own name matches - a language or a folder - keeps everything under it, because the reader asked
              // for the thing that holds it; and a branch that keeps something opens, since a filter that left the
              // answer inside a shut branch would show nothing.
              groups.slice().reverse().forEach(function (group) {
                if (summaryOf(group).indexOf(needle) >= 0) {
                  group.querySelectorAll('a.item').forEach(function (item) { show(item, true); });
                }

                var visible = group.querySelectorAll('a.item:not(.hidden)').length;

                group.classList.toggle('hidden', visible === 0);
                group.open = visible > 0;
              });

              document.body.classList.add('filtering');
            }

            box.addEventListener('input', apply);

            // Enter goes to the first entry that survived the filter, which is the point of typing a concept's name.
            box.addEventListener('keydown', function (event) {
              if (event.key !== 'Enter') { return; }

              var first = tree.querySelector('a.item:not(.hidden)');
              if (first) { window.location.href = first.getAttribute('href'); }
            });
          }

          document.addEventListener('DOMContentLoaded', function () {
            drawer();
            filter();
          });
        }());
        """;
}