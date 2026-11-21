<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9001-000000000002" name="Decl.Int">
  <nodes>
    <node id="d0000000-0000-4000-8001-000000000002" concept="Editor.Notation" name="Decl.Int">
      <properties>
        <conceptKey>Decl.Int</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="d0000000-0000-4000-8001-000000000102" concept="Editor.Line">
          <children role="cells">
            <node id="d0000000-0000-4000-8001-000000000202" concept="Editor.Property" name="value">
              <properties>
                <slot>value</slot>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000002002" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000002102" concept="Editor.Punct" name=":">
              <properties>
                <text>:</text>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000002202" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000002302" concept="Editor.TypeOf" />
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>