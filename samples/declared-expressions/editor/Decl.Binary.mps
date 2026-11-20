<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9001-000000000004" name="Decl.Binary">
  <nodes>
    <node id="d0000000-0000-4000-8001-000000000004" concept="Editor.Notation" name="Decl.Binary">
      <properties>
        <conceptKey>Decl.Binary</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="d0000000-0000-4000-8001-000000000104" concept="Editor.Line">
          <children role="cells">
            <node id="d0000000-0000-4000-8001-000000000204" concept="Editor.ChildInline" name="left">
              <properties>
                <slot>left</slot>
                <emptyText>&lt;empty&gt;</emptyText>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000000304" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000000404" concept="Editor.Property" name="operator">
              <properties>
                <slot>operator</slot>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000000504" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000000604" concept="Editor.ChildInline" name="right">
              <properties>
                <slot>right</slot>
                <emptyText>&lt;empty&gt;</emptyText>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>