<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="5ba81d94-de24-4a69-8c5b-4d907272aaab" name="editor1">
  <nodes>
    <node id="19fcb3d9-0d65-4fe6-a425-478f07403470" concept="Editor.Notation" name="Workflow.Workflow">
      <properties>
        <conceptKey>Workflow.Workflow</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="6cb042d4-58a2-4dbf-b423-18895dd13770" concept="Editor.Line">
          <children role="cells">
            <node id="b49bff21-1057-41e9-881a-491ce114ebb6" concept="Editor.Keyword" name="workflow">
              <properties>
                <text>workflow</text>
              </properties>
            </node>
            <node id="00ae941d-5766-460f-ad75-09c0f6d76a72" concept="Editor.Space" />
            <node id="1205000d-6440-4206-a599-77837c851325" concept="Editor.Property" name="name">
              <properties>
                <slot>name</slot>
              </properties>
            </node>
            <node id="fd17fab1-ae96-41a6-a68c-bd566f50460f" concept="Editor.Space" />
            <node id="b5d78400-dbe0-4bc7-92ce-73f989c16cca" concept="Editor.Punct" name="-&gt;">
              <properties>
                <text>-&gt;</text>
              </properties>
            </node>
            <node id="28385247-60fd-4756-b6e3-666732402ca4" concept="Editor.Space" />
            <node id="5697dce9-35b6-4e28-9e75-39673102f90a" concept="Editor.ChildInline" name="first">
              <properties>
                <slot>first</slot>
                <emptyText>&lt;empty&gt;</emptyText>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>