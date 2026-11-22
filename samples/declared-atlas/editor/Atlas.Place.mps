<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="e3000000-0000-4000-9000-000000000001" name="Atlas.Place">
  <nodes>
    <node id="e3000000-0000-4000-8000-000000000001" concept="Editor.Notation" name="Atlas.Place">
      <properties>
        <conceptKey>Atlas.Place</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="e3000000-0000-4000-8000-000000000002" concept="Editor.Line">
          <children role="cells">
            <node id="e3000000-0000-4000-8000-000000000003" concept="Editor.Keyword" name="place">
              <properties>
                <text>place</text>
              </properties>
            </node>
            <node id="e3000000-0000-4000-8000-000000000004" concept="Editor.Space" />
            <node id="e3000000-0000-4000-8000-000000000005" concept="Editor.Property" name="name">
              <properties>
                <slot>name</slot>
              </properties>
            </node>
            <node id="e3000000-0000-4000-8000-000000000006" concept="Editor.Space" />
            <node id="e3000000-0000-4000-8000-000000000007" concept="Editor.Keyword" name="at">
              <properties>
                <text>at</text>
              </properties>
            </node>
            <node id="e3000000-0000-4000-8000-000000000008" concept="Editor.Space" />
            <node id="e3000000-0000-4000-8000-000000000009" concept="Editor.Property" name="at">
              <properties>
                <slot>at</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>