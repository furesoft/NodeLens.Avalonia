<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="72c43ccd-8cc8-4952-b597-74b6216184d3" name="Pipeline.Pipeline">
  <nodes>
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
  </nodes>
</model>