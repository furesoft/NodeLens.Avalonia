<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9001-000000000005" name="Decl.If">
  <nodes>
    <node id="d0000000-0000-4000-8001-000000000005" concept="Editor.Notation" name="Decl.If">
      <properties>
        <conceptKey>Decl.If</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="d0000000-0000-4000-8001-000000000105" concept="Editor.Line">
          <children role="cells">
            <node id="d0000000-0000-4000-8001-000000000205" concept="Editor.Keyword" name="if">
              <properties>
                <text>if</text>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000000305" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000000405" concept="Editor.ChildInline" name="condition">
              <properties>
                <slot>condition</slot>
                <emptyText>&lt;empty&gt;</emptyText>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000000505" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000000605" concept="Editor.Keyword" name="then">
              <properties>
                <text>then</text>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000000705" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000000805" concept="Editor.ChildInline" name="then">
              <properties>
                <slot>then</slot>
                <emptyText>&lt;empty&gt;</emptyText>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000000905" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000000a05" concept="Editor.Keyword" name="else">
              <properties>
                <text>else</text>
              </properties>
            </node>
            <node id="d0000000-0000-4000-8001-000000000b05" concept="Editor.Space" />
            <node id="d0000000-0000-4000-8001-000000000c05" concept="Editor.ChildInline" name="else">
              <properties>
                <slot>else</slot>
                <emptyText>&lt;empty&gt;</emptyText>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>