<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="8e7f6d5c-4b3a-4291-8a0f-1c2d3e4f5a60" name="editor3">
  <nodes>
    <node id="d4e5f6a7-b8c9-4da3-8eb4-5f6a7b8c9d02" concept="Editor.Notation" name="Workflow.Call">
      <properties>
        <conceptKey>Workflow.Call</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="e5f6a7b8-c9da-4eb4-9fc5-6a7b8c9d0e13" concept="Editor.Line">
          <children role="cells">
            <node id="f6a7b8c9-daeb-4fc5-80d6-7b8c9d0e1f24" concept="Editor.Property" name="name">
              <properties>
                <slot>name</slot>
              </properties>
            </node>
            <node id="a7b8c9da-ebfc-40d6-91e7-8c9d0e1f2a35" concept="Editor.Space" />
            <node id="b8c9daeb-fc0d-41e7-82f8-9d0e1f2a3b46" concept="Editor.Punct" name=":">
              <properties>
                <text>:</text>
              </properties>
            </node>
            <node id="c9daebfc-0d1e-42f8-83a9-0e1f2a3b4c57" concept="Editor.Space" />
            <node id="daebfc0d-1e2f-43a9-84ba-1f2a3b4c5d68" concept="Editor.Reference" name="calls">
              <properties>
                <slot>calls</slot>
              </properties>
            </node>
            <node id="ebfc0d1e-2f3a-44ba-85cb-2a3b4c5d6e79" concept="Editor.Space" />
            <node id="fc0d1e2f-3a4b-45cb-86dc-3b4c5d6e7f8a" concept="Editor.Punct" name="then">
              <properties>
                <text>then</text>
              </properties>
            </node>
            <node id="0d1e2f3a-4b5c-46dc-87ed-4c5d6e7f8a9b" concept="Editor.Space" />
            <node id="1e2f3a4b-5c6d-47ed-88fe-5d6e7f8a9b0c" concept="Editor.ChildInline" name="next">
              <properties>
                <slot>next</slot>
                <emptyText>&lt;empty&gt;</emptyText>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>