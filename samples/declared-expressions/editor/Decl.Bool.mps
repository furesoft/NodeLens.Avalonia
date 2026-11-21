<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9001-000000000003" name="Decl.Bool">
  <nodes>
    <node id="d0000000-0000-4000-8001-000000000003" concept="Editor.Notation" name="Decl.Bool">
      <properties>
        <conceptKey>Decl.Bool</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="d0000000-0000-4000-8001-000000000103" concept="Editor.Line">
          <children role="cells">
            <node id="d0000000-0000-4000-8001-000000000203" concept="Editor.Property" name="value">
              <properties>
                <slot>value</slot>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000002003" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000002103" concept="Editor.Punct" name=":">
              <properties>
                <text>:</text>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000002203" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000002303" concept="Editor.TypeOf" />
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>