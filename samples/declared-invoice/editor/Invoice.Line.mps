<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f0000000-0000-4000-9000-000000000003" name="Invoice.Line">
  <nodes>
    <node id="f0000000-0000-4000-8000-000000000003" concept="Editor.Notation" name="Invoice.Line">
      <properties>
        <conceptKey>Invoice.Line</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="f0000000-0000-4000-8000-000000000203" concept="Editor.Line">
          <children role="cells">
            <node id="f0000000-0000-4000-8000-000000000303" concept="Editor.Property" name="label">
              <properties>
                <slot>label</slot>
              </properties>
            </node>
            <node id="f0000000-0000-4000-8000-000000000403" concept="Editor.Space" />
            <node id="f0000000-0000-4000-8000-000000000503" concept="Editor.Punct" name="=">
              <properties>
                <text>=</text>
              </properties>
            </node>
            <node id="f0000000-0000-4000-8000-000000000603" concept="Editor.Space" />
            <node id="f0000000-0000-4000-8000-000000000703" concept="Editor.ChildInline" name="quantity">
              <properties>
                <slot>quantity</slot>
                <emptyText>&lt;quantity&gt;</emptyText>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>