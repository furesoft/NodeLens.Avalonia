# `Projectional.Xml`

> Part of [import.md](../import.md), the design of record for text import. This file is one
> reference language; the contract, the failure discipline, the conversion rule, the slices and the
> open questions stay in the parent.


| Concept | Slots | Notes |
| --- | --- | --- |
| `Xml.Node` | - | abstract: the children of an element |
| `Xml.Element` | `name`: string, `attributes`: many `Xml.Attribute`, `children`: many `Xml.Node` | |
| `Xml.Attribute` | `name`: string, `value`: string | order preserved |
| `Xml.Text` | `text`: string | the **canonical escaped spelling**, whitespace-only runs included |
| `Xml.Comment` | `text`: string | |
| `Xml.Raw` | `text`: string | tier 2: processing instructions, CDATA, DOCTYPE |

- **Namespaces are text.** `p:name` is the name; there is no namespace resolution, no prefix
  table, no `xmlns` handling beyond "it is an attribute like any other". Resolving them is a
  schema feature, not an editor feature.
- **Escaping happens on the way in, not on the way out.** The parser hands back a decoded value,
  and the importer writes the canonical spelling into the model (`&amp;` `&lt;` `&gt;` `&quot;`, and
  `&#x9;`/`&#xA;`/`&#xD;` in an attribute, where a literal one would be normalised to a space). So a
  template emits a slot verbatim, exactly as the JSON language emits a string, and the framework's
  template vocabulary needs no XML-specific part. The price is the same one JSON pays: the editor shows
  the spelling, so a `&` is typed as `&amp;`. A file that wrote `&#38;` comes back as `&amp;` - one
  canonical spelling for one character, which is what a byte-exact corpus can compare against.
- **An element with no content is written `<a></a>`.** A model holds one shape for it, so it cannot
  also remember which of the two spellings the file used, and a reference language must not have two
  spellings for one tree. A file written `<a/>` comes back in the long form, which every XML consumer
  reads as the same document.
- **A document is its top-level nodes.** XML allows comments and processing instructions outside the
  root element and they are content, so the importer answers several roots in document order rather
  than inventing a container. Only the XML declaration is not modelled: it describes the bytes a host
  read, not the tree, and the host that writes the generated text states it again.
- **The notation hides an empty collection.** In JSON an empty slot can be drawn as a blank; in XML it
  cannot, because whitespace inside an element *is* content - a placeholder would make an empty element
  indistinguishable from one holding a space, and that distinction is the language's whole job. The
  price is that the first attribute and the first child have no caret stop, which is exactly the pair
  the notation owes an intention for (see below).

- **Whitespace is content.** `XDocument.Load` with `LoadOptions.PreserveWhitespace`, and
  `Xml.Text` stores the run verbatim. XML consumers that care about `xml:space` would otherwise
  see their file rewritten by the editor.
- **External entities are refused.** The reader is configured with `DtdProcessing.Prohibit`
  and `XmlResolver = null`, and a document that needs either is rejected with
  `xml.external-entity-refused`. An editor that dereferences a `SYSTEM` identifier on import is
  an XXE vulnerability in every host that embeds it. This is not a limitation to lift later.
- **Attribute order is preserved**, because `XDocument` preserves it and because some tools
  diff attributes positionally.

