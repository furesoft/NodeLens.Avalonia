<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="a6f55f51-2837-49c1-ace2-e0e08316a7a6" name="Pipeline.Stage">
  <nodes>
    <node id="878dfa85-ab34-4fce-aa77-aa1f6e9c19f2" concept="Structure.Concept" name="Pipeline.Stage">
      <properties>
        <key>Pipeline.Stage</key>
        <isAbstract>false</isAbstract>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>One step of a pipeline.</description>
        <nameSlot>name</nameSlot>
      </properties>
      <children role="properties">
        <node id="b826c941-d464-4e7c-8b64-c47018da11cf" concept="Structure.PropertyDecl" name="name">
          <properties>
            <name>name</name>
            <typeName>string</typeName>
            <defaultValue>unnamed</defaultValue>
          </properties>
        </node>
        <node id="c5d12f99-423a-4fee-a67f-79528e321d23" concept="Structure.PropertyDecl" name="command">
          <properties>
            <name>command</name>
            <typeName>string</typeName>
          </properties>
        </node>
        <node id="fc3058de-f44c-4c1b-b737-d22ee91defd2" concept="Structure.PropertyDecl" name="retries">
          <properties>
            <name>retries</name>
            <typeName>int</typeName>
            <defaultValue>0</defaultValue>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>