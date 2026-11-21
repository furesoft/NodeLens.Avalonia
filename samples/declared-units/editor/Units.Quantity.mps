<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="e0000000-0000-4000-9000-000000000003" name="Units.Quantity">
  <nodes>
    <node id="e0000000-0000-4000-8000-000000000003" concept="Editor.Notation" name="Units.Quantity">
      <properties>
        <conceptKey>Units.Quantity</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="e0000000-0000-4000-8000-000000000203" concept="Editor.Line">
          <children role="cells">
            <node id="e0000000-0000-4000-8000-000000000303" concept="Editor.Property" name="value">
              <properties>
                <slot>value</slot>
              </properties>
            </node>
            <node id="e0000000-0000-4000-8000-000000000403" concept="Editor.Space" />
            <node id="e0000000-0000-4000-8000-000000000503" concept="Editor.Keyword" name="in">
              <properties>
                <text>in</text>
              </properties>
            </node>
            <node id="e0000000-0000-4000-8000-000000000603" concept="Editor.Space" />
            <node id="e0000000-0000-4000-8000-000000000703" concept="Editor.Property" name="unit">
              <properties>
                <slot>unit</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>