<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="e1000000-0000-4000-9000-000000000001" name="Atlas.Map">
  <nodes>
    <node id="e1000000-0000-4000-8000-000000000001" concept="Editor.Notation" name="Atlas.Map">
      <properties>
        <conceptKey>Atlas.Map</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="e1000000-0000-4000-8000-000000000002" concept="Editor.Sequence">
          <children role="cells">
            <node id="e1000000-0000-4000-8000-000000000003" concept="Editor.Line">
              <children role="cells">
                <node id="e1000000-0000-4000-8000-000000000004" concept="Editor.Keyword" name="map">
                  <properties>
                    <text>map</text>
                  </properties>
                </node>
                <node id="e1000000-0000-4000-8000-000000000005" concept="Editor.Space" />
                <node id="e1000000-0000-4000-8000-000000000006" concept="Editor.Property" name="name">
                  <properties>
                    <slot>name</slot>
                  </properties>
                </node>
              </children>
            </node>
            <node id="e1000000-0000-4000-8000-000000000007" concept="Editor.Indent">
              <children role="cells">
                <node id="e1000000-0000-4000-8000-000000000008" concept="Editor.Children" name="places">
                  <properties>
                    <slot>places</slot>
                    <emptyText>&lt;none&gt;</emptyText>
                    <spacing>1</spacing>
                    <stacked>true</stacked>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>