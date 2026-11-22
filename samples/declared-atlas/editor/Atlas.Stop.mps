<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="e5000000-0000-4000-9000-000000000001" name="Atlas.Stop">
  <nodes>
    <node id="e5000000-0000-4000-8000-000000000001" concept="Editor.Notation" name="Atlas.Stop">
      <properties>
        <conceptKey>Atlas.Stop</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="e5000000-0000-4000-8000-000000000002" concept="Editor.Line">
          <children role="cells">
            <node id="e5000000-0000-4000-8000-000000000003" concept="Editor.Keyword" name="to">
              <properties>
                <text>to</text>
              </properties>
            </node>
            <node id="e5000000-0000-4000-8000-000000000004" concept="Editor.Space" />
            <node id="e5000000-0000-4000-8000-000000000005" concept="Editor.Reference" name="to">
              <properties>
                <slot>to</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>