<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f0000000-0000-4000-9000-000000000006" name="Invoice.Bill">
  <nodes>
    <node id="f0000000-0000-4000-8000-000000000006" concept="Editor.Notation" name="Invoice.Bill">
      <properties>
        <conceptKey>Invoice.Bill</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="f0000000-0000-4000-8000-000000000016" concept="Editor.Sequence">
          <children role="cells">
            <node id="f0000000-0000-4000-8000-000000000026" concept="Editor.Line">
              <children role="cells">
                <node id="f0000000-0000-4000-8000-000000000036" concept="Editor.Keyword" name="bill">
                  <properties>
                    <text>bill</text>
                  </properties>
                </node>
                <node id="f0000000-0000-4000-8000-000000000046" concept="Editor.Space" />
                <node id="f0000000-0000-4000-8000-000000000056" concept="Editor.Property" name="name">
                  <properties>
                    <slot>name</slot>
                  </properties>
                </node>
              </children>
            </node>
            <node id="f0000000-0000-4000-8000-000000000066" concept="Editor.Indent">
              <children role="cells">
                <node id="f0000000-0000-4000-8000-000000000076" concept="Editor.Children" name="lines">
                  <properties>
                    <slot>lines</slot>
                    <emptyText>&lt;none&gt;</emptyText>
                    <spacing>0</spacing>
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