<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="10b5b316-24a7-40e1-9a0a-c430a9cc39fa" name="structure">
  <nodes>
    <node id="85be479e-cb6a-4945-a676-616e0c6a7b02" concept="Structure.Language">
      <children role="concepts">
        <node id="37faca7a-cd01-422f-b3f1-f102bdff0803" concept="Structure.Concept" name="Pipeline.Pipeline">
          <properties>
            <key>Pipeline.Pipeline</key>
            <isAbstract>false</isAbstract>
            <isOfferedInCompletion>true</isOfferedInCompletion>
            <description>A named sequence of stages.</description>
            <nameSlot>name</nameSlot>
          </properties>
          <children role="properties">
            <node id="82f91b42-c371-40b0-bef2-d5cae86e2e9d" concept="Structure.PropertyDecl" name="name">
              <properties>
                <name>name</name>
                <typeName>string</typeName>
                <defaultValue>unnamed</defaultValue>
              </properties>
            </node>
          </children>
          <children role="children">
            <node id="208456f7-d477-4b60-b45c-3b9e6d37f579" concept="Structure.ChildDecl" name="stages">
              <properties>
                <name>stages</name>
                <childConceptKey>Pipeline.Stage</childConceptKey>
                <cardinality>many</cardinality>
              </properties>
            </node>
          </children>
        </node>
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
      </children>
    </node>
  </nodes>
</model>