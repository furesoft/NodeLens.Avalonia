<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b558d358-b6fe-4dd2-a21c-1888bb53f863" name="Pipeline.Stage">
  <nodes>
    <node id="ff711c8d-3e6a-4aa6-b089-985b477aeca3" concept="Editor.Notation" name="Pipeline.Stage">
      <properties>
        <conceptKey>Pipeline.Stage</conceptKey>
        <variant>default</variant>
      </properties>
      <children role="root">
        <node id="73eb371c-13e9-4015-9001-75932d09b364" concept="Editor.Line">
          <children role="cells">
            <node id="2f2957d2-aa8a-4baa-a381-c3173159bc5d" concept="Editor.Property" name="name">
              <properties>
                <slot>name</slot>
              </properties>
            </node>
            <node id="d386884e-f237-4483-b741-4f57e1338836" concept="Editor.Space" />
            <node id="ee537410-1725-4520-bd4a-f65d4aaa1977" concept="Editor.Property" name="command">
              <properties>
                <slot>command</slot>
              </properties>
            </node>
            <node id="a1145a04-6130-42bb-8050-159f8629ea58" concept="Editor.Space" />
            <node id="ca93e6d5-c533-409a-aecd-f751efa6bac1" concept="Editor.Property" name="retries">
              <properties>
                <slot>retries</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>