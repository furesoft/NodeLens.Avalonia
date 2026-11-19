<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="0a0664cb-e573-43d8-83d4-e72f796238fe" name="editor2">
  <nodes>
    <node id="d156e887-8344-4c3a-89b2-4ee70ef0a051" concept="Editor.Notation" name="Workflow.Step">
      <properties>
        <conceptKey>Workflow.Step</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="68feaf6c-e77c-4a2c-859a-3687686a01db" concept="Editor.Line">
          <children role="cells">
            <node id="318d210b-ae3a-4043-a198-8a141d7a9526" concept="Editor.Property" name="name">
              <properties>
                <slot>name</slot>
              </properties>
            </node>
            <node id="447aeeac-badf-4937-bf27-4948b88545c7" concept="Editor.Space" />
            <node id="bcf50a7b-44e7-4523-b431-b32bb790d4d2" concept="Editor.Punct" name=":">
              <properties>
                <text>:</text>
              </properties>
            </node>
            <node id="05a23e76-a664-41b5-9f45-db445a81a541" concept="Editor.Space" />
            <node id="fa4c042f-2ef2-4c17-8c2c-838904679934" concept="Editor.Property" name="command">
              <properties>
                <slot>command</slot>
              </properties>
            </node>
            <node id="dee6cdd5-0863-416e-bae7-32fe0637d8e8" concept="Editor.Space" />
            <node id="fa0b089a-34ae-42d5-93f7-d6dfa651270f" concept="Editor.Reference" name="onFailure">
              <properties>
                <slot>onFailure</slot>
              </properties>
            </node>
            <node id="0c344d34-d966-47bf-b2ac-8a76b7741dec" concept="Editor.Space" />
            <node id="935a4658-4f44-442a-8910-ca4f81f4758e" concept="Editor.Punct" name="then">
              <properties>
                <text>then</text>
              </properties>
            </node>
            <node id="98565f67-37ab-4bfd-aefc-385b0bba08b0" concept="Editor.Space" />
            <node id="50f12a7b-6924-4a31-b674-ff3badf3986c" concept="Editor.ChildInline" name="next">
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